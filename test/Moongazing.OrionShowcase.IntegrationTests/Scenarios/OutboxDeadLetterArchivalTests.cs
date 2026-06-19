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
    public async Task A_replayed_terminal_path_that_races_the_insert_is_a_conflict_tolerant_no_op()
    {
        // Models a lease-expiry/crash-replay where the SAME message id reaches a terminal path twice:
        // the first pass dead-letters and removes the source row, then a stale replay re-appears in the
        // hot outbox (same id) and dead-letters again. The second insert collides with the dead-letter
        // primary key; the store must catch the unique/PK violation, treat it as already dead-lettered
        // (no-op success), NOT throw, and still leave the hot outbox clean.
        using var scope = _fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        var deadLetterStore = scope.ServiceProvider.GetRequiredService<IDeadLetterStore>();

        var rowId = await SeedRowAsync(db, OutboxStatus.Pending, attemptCount: 5);
        var context = new DeadLetterContext("delivery failed after 5 attempts", AttemptCount: 5, DateTime.UtcNow);

        var first = await deadLetterStore.DeadLetterAsync(rowId, context, CancellationToken.None);
        first.Should().BeTrue("the first terminal pass performs the move");

        // A stale replay re-materialises the SAME id in the hot outbox while the dead-letter row already
        // exists, so the next DeadLetterAsync insert collides with the dead-letter primary key. This is
        // the exact non-idempotent crash the old pre-check-then-insert path threw on.
        db.ChangeTracker.Clear();
        await SeedRowWithIdAsync(db, rowId, OutboxStatus.Pending, attemptCount: 5);

        bool replayResult = true;
        Func<Task> replay = async () =>
            replayResult = await deadLetterStore.DeadLetterAsync(rowId, context, CancellationToken.None);

        await replay.Should().NotThrowAsync("a duplicate dead-letter insert is conflict-tolerant, not an error");
        replayResult.Should().BeFalse("the replayed pass is a no-op success, not a second routing");

        (await db.Set<OutboxRow>().AnyAsync(x => x.Id == rowId)).Should().BeFalse("the replayed source row is removed from the hot outbox");
        (await deadLetterStore.GetDeadLetteredAsync(CancellationToken.None))
            .Count(x => x.Id == rowId).Should().Be(1, "the message remains dead-lettered exactly once");
    }

    [Fact]
    public async Task Archival_deletes_exactly_the_snapshotted_ids_not_a_re_evaluated_time_predicate()
    {
        // Guards against the snapshot/delete race: the archival SELECT snapshots due ids, then the DELETE
        // must remove EXACTLY those ids. A re-evaluated broad time predicate could also match rows that
        // became due after the snapshot and delete them WITHOUT archiving (data loss). Here every reaped
        // id must be present in the archive.
        using var scope = _fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        var archival = scope.ServiceProvider.GetRequiredService<IOutboxArchivalStore>();

        var now = DateTime.UtcNow;
        var retention = TimeSpan.FromDays(7);

        var dueA = await SeedRowAsync(db, OutboxStatus.Processed, processedAtUtc: now - TimeSpan.FromDays(10));
        var dueB = await SeedRowAsync(db, OutboxStatus.Processed, processedAtUtc: now - TimeSpan.FromDays(9));
        var insideRetention = await SeedRowAsync(db, OutboxStatus.Processed, processedAtUtc: now - TimeSpan.FromDays(1));

        var moved = await archival.ArchiveProcessedAsync(retention, now, CancellationToken.None);
        moved.Should().BeGreaterThanOrEqualTo(2);

        var archived = await archival.GetArchivedAsync(CancellationToken.None);

        // Every due row that left the hot outbox is provably in the archive (no delete-without-archive).
        foreach (var id in new[] { dueA, dueB })
        {
            (await db.Set<OutboxRow>().AnyAsync(x => x.Id == id)).Should().BeFalse("a snapshotted due row is reaped");
            archived.Should().Contain(x => x.Id == id, "every reaped row is archived, never deleted without archival");
        }

        (await db.Set<OutboxRow>().AnyAsync(x => x.Id == insideRetention)).Should().BeTrue("a row inside retention is not snapshotted, so not deleted");
        archived.Should().NotContain(x => x.Id == insideRetention);
    }

    private static async Task SeedRowWithIdAsync(BankingDbContext db, Guid id, OutboxStatus status, int attemptCount)
    {
        db.Set<OutboxRow>().Add(new OutboxRow
        {
            Id = id,
            MessageType = "transfer.completed",
            Payload = "{}",
            OccurredAtUtc = DateTime.UtcNow,
            EnqueuedAtUtc = DateTime.UtcNow,
            Status = status,
            AttemptCount = attemptCount,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
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
