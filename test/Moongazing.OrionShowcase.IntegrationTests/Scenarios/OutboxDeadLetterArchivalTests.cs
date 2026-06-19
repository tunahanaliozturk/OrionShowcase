namespace Moongazing.OrionShowcase.IntegrationTests.Scenarios;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionPatch.Abstractions;
using Moongazing.OrionPatch.Models;
using Moongazing.OrionShowcase.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

/// <summary>
/// Upgrade 2 (Postgres-backed): OrionPatch 0.3 outbox dead-letter store + archival, exercised against
/// the real Npgsql provider the showcase runs in production. Seeds outbox rows directly, then drives
/// the registered <see cref="IDeadLetterStore"/> / <see cref="IOutboxArchivalStore"/> (the same
/// composite storage the dispatcher uses) to prove: an exhausted row is dead-lettered exactly once
/// and removed from the hot outbox; dispatched rows past the retention window are archived while
/// pending and dead-lettered rows are untouched.
/// </summary>
public class OutboxDeadLetterArchivalTests : IClassFixture<BankingApiFixture>
{
    private readonly BankingApiFixture _fx;

    public OutboxDeadLetterArchivalTests(BankingApiFixture fx) => _fx = fx;

    private static async Task<Guid> SeedRowAsync(BankingDbContext db, OutboxStatus status, DateTime? processedAtUtc = null, int attemptCount = 0)
    {
        var id = Guid.NewGuid();
        db.Set<OutboxRow>().Add(new OutboxRow
        {
            Id = id,
            MessageType = "transfer.completed",
            Payload = "{}",
            OccurredAtUtc = DateTime.UtcNow,
            EnqueuedAtUtc = DateTime.UtcNow,
            Status = status,
            AttemptCount = attemptCount,
            ProcessedAtUtc = processedAtUtc,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return id;
    }

    [Fact]
    public async Task An_exhausted_row_is_dead_lettered_once_and_not_retried()
    {
        using var scope = _fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        var deadLetterStore = scope.ServiceProvider.GetRequiredService<IDeadLetterStore>();

        var rowId = await SeedRowAsync(db, OutboxStatus.Pending, attemptCount: 5);
        var context = new DeadLetterContext("delivery failed after 5 attempts", AttemptCount: 5, DateTime.UtcNow);

        var first = await deadLetterStore.DeadLetterAsync(rowId, context, CancellationToken.None);
        first.Should().BeTrue("the first terminal pass performs the move");

        (await db.Set<OutboxRow>().AnyAsync(x => x.Id == rowId)).Should().BeFalse("the source row left the hot outbox");

        var deadLettered = await deadLetterStore.GetDeadLetteredAsync(CancellationToken.None);
        deadLettered.Should().ContainSingle(x => x.Id == rowId)
            .Which.AttemptCount.Should().Be(5);

        // Idempotent: a second terminal pass for the same row does not route it again.
        var second = await deadLetterStore.DeadLetterAsync(rowId, context, CancellationToken.None);
        second.Should().BeFalse("a row already dead-lettered is not routed a second time");
        (await deadLetterStore.GetDeadLetteredAsync(CancellationToken.None))
            .Count(x => x.Id == rowId).Should().Be(1);
    }

    [Fact]
    public async Task Dispatched_rows_are_archived_per_retention_while_pending_ones_are_untouched()
    {
        using var scope = _fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        var archival = scope.ServiceProvider.GetRequiredService<IOutboxArchivalStore>();

        var now = DateTime.UtcNow;
        var retention = TimeSpan.FromDays(7);

        var oldProcessed = await SeedRowAsync(db, OutboxStatus.Processed, processedAtUtc: now - TimeSpan.FromDays(10));
        var recentProcessed = await SeedRowAsync(db, OutboxStatus.Processed, processedAtUtc: now - TimeSpan.FromDays(1));
        var pending = await SeedRowAsync(db, OutboxStatus.Pending);
        var deadLettered = await SeedRowAsync(db, OutboxStatus.DeadLettered);

        var moved = await archival.ArchiveProcessedAsync(retention, now, CancellationToken.None);
        moved.Should().BeGreaterThanOrEqualTo(1, "at least the old processed row past retention is reaped");

        (await db.Set<OutboxRow>().AnyAsync(x => x.Id == oldProcessed)).Should().BeFalse("the old processed row left the hot outbox");
        (await db.Set<OutboxRow>().AnyAsync(x => x.Id == recentProcessed)).Should().BeTrue("a processed row inside retention stays");
        (await db.Set<OutboxRow>().AnyAsync(x => x.Id == pending)).Should().BeTrue("a pending row is never archived");
        (await db.Set<OutboxRow>().AnyAsync(x => x.Id == deadLettered)).Should().BeTrue("a dead-lettered row is never archived");

        var archived = await archival.GetArchivedAsync(CancellationToken.None);
        archived.Should().Contain(x => x.Id == oldProcessed);
    }
}
