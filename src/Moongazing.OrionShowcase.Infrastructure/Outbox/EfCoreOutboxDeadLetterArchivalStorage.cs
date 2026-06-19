namespace Moongazing.OrionShowcase.Infrastructure.Outbox;

using Microsoft.EntityFrameworkCore;
using Moongazing.OrionPatch.Abstractions;
using Moongazing.OrionPatch.EntityFrameworkCore;
using Moongazing.OrionPatch.Models;
using Moongazing.OrionShowcase.Infrastructure.Persistence;

/// <summary>
/// EF Core outbox storage that augments the bundled <see cref="EfCoreOutboxStorage"/> with the
/// OrionPatch 0.3 dead-letter and archival capabilities. The dispatcher routes an exhausted row to
/// <see cref="IDeadLetterStore"/> only when the injected <see cref="IOutboxStorage"/> also implements
/// it; the stock EF storage does not, so this type composes over it and implements both
/// <see cref="IDeadLetterStore"/> and <see cref="IOutboxArchivalStore"/>. It is registered as
/// <see cref="IOutboxStorage"/> (replacing the stock storage) so the dispatcher's terminal path uses
/// the durable dead-letter store instead of the in-place <c>Status = DeadLettered</c> fallback.
/// </summary>
/// <remarks>
/// <para>
/// Dead-lettering and archival both perform a move within a single <c>SaveChanges</c> against the
/// bound <see cref="BankingDbContext"/>: the source row is removed from the hot
/// <c>orion_patch_outbox</c> table and the projected row is appended to the sibling
/// <c>orion_patch_dead_letter</c> / <c>orion_patch_outbox_archive</c> table. The move is idempotent
/// on the row id, so a redelivered terminal path (lease expiry, crash-replay) lands a message in the
/// store exactly once.
/// </para>
/// <para>
/// Scoped, like the storage it composes, because it depends on the scoped DbContext.
/// </para>
/// </remarks>
public sealed class EfCoreOutboxDeadLetterArchivalStorage : IOutboxStorage, IDeadLetterStore, IOutboxArchivalStore
{
    private readonly BankingDbContext _db;
    private readonly EfCoreOutboxStorage _inner;

    public EfCoreOutboxDeadLetterArchivalStorage(BankingDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
        _inner = new EfCoreOutboxStorage(db);
    }

    // IOutboxStorage: delegate the hot-path operations to the bundled EF storage verbatim.
    public Task AppendAsync(IReadOnlyList<OutboxRow> rows, CancellationToken cancellationToken = default)
        => _inner.AppendAsync(rows, cancellationToken);

    public Task<IReadOnlyList<OutboxRow>> ClaimNextAsync(
        int batchSize, string dispatcherIdentity, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
        => _inner.ClaimNextAsync(batchSize, dispatcherIdentity, leaseDuration, cancellationToken);

    public Task CompleteAsync(Guid rowId, DateTime processedAtUtc, CancellationToken cancellationToken = default)
        => _inner.CompleteAsync(rowId, processedAtUtc, cancellationToken);

    public Task FailAsync(Guid rowId, string errorMessage, DateTime nextAttemptAtUtc, CancellationToken cancellationToken = default)
        => _inner.FailAsync(rowId, errorMessage, nextAttemptAtUtc, cancellationToken);

    public Task DeadLetterAsync(Guid rowId, string errorMessage, CancellationToken cancellationToken = default)
        => _inner.DeadLetterAsync(rowId, errorMessage, cancellationToken);

    public Task<long> QueueDepthAsync(CancellationToken cancellationToken = default)
        => _inner.QueueDepthAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> DeadLetterAsync(Guid rowId, DeadLetterContext context, CancellationToken cancellationToken = default)
    {
        // The dead-letter table's id is its primary key, copied verbatim from the source outbox row,
        // so the PK itself is the per-message uniqueness guard: at most one dead-letter row can exist
        // per message id. We rely on that constraint instead of a non-atomic pre-check. A pre-check
        // (SELECT ... ANY then INSERT) is a TOCTOU race: under a lease-expiry/crash-replay two terminal
        // paths can both observe "not yet dead-lettered" and both attempt the insert, and the second
        // throws. Here the insert is attempted optimistically and a unique/PK violation is treated as
        // "already dead-lettered by a concurrent/replayed terminal path" (a no-op success), which is the
        // idempotent, conflict-tolerant behaviour the terminal path requires.

        var source = await _db.Set<OutboxRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == rowId, cancellationToken)
            .ConfigureAwait(false);
        if (source is null)
        {
            // Source row no longer exists: either it was never claimed or a prior terminal pass already
            // moved it. Nothing to move; report no move performed.
            return false;
        }

        var entity = new OutboxDeadLetterEntity
        {
            Id = source.Id,
            MessageType = source.MessageType,
            Payload = source.Payload,
            HeadersJson = source.HeadersJson,
            CorrelationId = source.CorrelationId,
            OccurredAtUtc = source.OccurredAtUtc,
            EnqueuedAtUtc = source.EnqueuedAtUtc,
            AttemptCount = context.AttemptCount,
            FinalError = context.FinalError,
            DeadLetteredAtUtc = context.DeadLetteredAtUtc,
        };

        try
        {
            // Move atomically: append to the dead-letter table and remove the source row from the hot
            // outbox in one transaction so the active outbox is never polluted with terminal rows and a
            // crash cannot leave the message in both tables or neither.
            await ExecuteInTransactionAsync(async ct =>
            {
                await _db.Set<OutboxDeadLetterEntity>().AddAsync(entity, ct).ConfigureAwait(false);
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                await _db.Set<OutboxRow>()
                    .Where(x => x.Id == rowId)
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (DbUpdateException)
        {
            // A concurrent/replayed terminal path already inserted the dead-letter row for this id; the
            // PK rejected our insert. Treat it as already dead-lettered (idempotent no-op success) and
            // still ensure the source row is removed from the hot outbox, in case the winning path
            // crashed between its insert and its delete.
            _db.ChangeTracker.Clear();
            await _db.Set<OutboxRow>()
                .Where(x => x.Id == rowId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeadLetteredMessage>> GetDeadLetteredAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.Set<OutboxDeadLetterEntity>()
            .AsNoTracking()
            .OrderBy(x => x.DeadLetteredAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.ConvertAll(x => x.ToModel());
    }

    /// <inheritdoc />
    public async Task<int> ArchiveProcessedAsync(TimeSpan retention, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(retention, TimeSpan.Zero);

        var cutoff = nowUtc - retention;

        // Only Processed rows whose dispatch instant is at or before the cutoff are reaped. Pending,
        // Claimed, and DeadLettered rows are never touched, and a processed row still inside the
        // retention window is never touched.
        var due = await _db.Set<OutboxRow>()
            .AsNoTracking()
            .Where(x => x.Status == OutboxStatus.Processed
                && x.ProcessedAtUtc != null
                && x.ProcessedAtUtc <= cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (due.Count == 0)
        {
            return 0;
        }

        var archived = due.ConvertAll(source => new OutboxArchiveEntity
        {
            Id = source.Id,
            MessageType = source.MessageType,
            Payload = source.Payload,
            HeadersJson = source.HeadersJson,
            CorrelationId = source.CorrelationId,
            OccurredAtUtc = source.OccurredAtUtc,
            EnqueuedAtUtc = source.EnqueuedAtUtc,
            AttemptCount = source.AttemptCount,
            LastError = source.LastError,
            ProcessedAtUtc = source.ProcessedAtUtc,
            ArchivedAtUtc = nowUtc,
        });

        var dueIds = due.ConvertAll(x => x.Id);

        // Idempotent/incremental: skip ids already in the archive (a prior pass moved them), then move
        // the rest out of the hot outbox in one SaveChanges.
        var existing = await _db.Set<OutboxArchiveEntity>()
            .AsNoTracking()
            .Where(x => dueIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingSet = existing.ToHashSet();

        var toAdd = archived.FindAll(x => !existingSet.Contains(x.Id));

        // Move atomically: append the newly archived rows and remove the reaped processed rows from
        // the hot outbox in one transaction so a crash cannot drop a row from both tables.
        //
        // The DELETE is constrained to EXACTLY the snapshotted ids (id IN dueIds), never the broad
        // time predicate. Re-evaluating the time predicate here would also match rows that became due
        // AFTER the SELECT (dispatched between the snapshot and this delete) - those are NOT in the
        // archive yet, so deleting them would lose data. Every id in dueIds is provably archived by
        // this point (either already present, or appended via toAdd above), so deleting precisely those
        // ids removes only rows we have durably preserved.
        var moved = 0;
        await ExecuteInTransactionAsync(async ct =>
        {
            if (toAdd.Count > 0)
            {
                await _db.Set<OutboxArchiveEntity>().AddRangeAsync(toAdd, ct).ConfigureAwait(false);
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            moved = await _db.Set<OutboxRow>()
                .Where(x => dueIds.Contains(x.Id))
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return moved;
    }

    // Runs the move under the context's execution strategy + an explicit transaction so the append
    // and the delete commit together. ExecuteDeleteAsync issues its own statement immediately, so a
    // surrounding transaction is what makes the two halves atomic.
    private async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            var tx = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using (tx.ConfigureAwait(false))
            {
                await action(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxRow>> GetArchivedAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.Set<OutboxArchiveEntity>()
            .AsNoTracking()
            .OrderBy(x => x.ArchivedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.ConvertAll(x => x.ToModel());
    }
}
