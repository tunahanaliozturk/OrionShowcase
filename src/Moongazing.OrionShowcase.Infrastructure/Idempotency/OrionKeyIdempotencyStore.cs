namespace Moongazing.OrionShowcase.Infrastructure.Idempotency;

using Microsoft.EntityFrameworkCore;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Infrastructure.Persistence;

/// <summary>
/// EF-backed <see cref="IIdempotencyStore"/>. OrionKey 0.4.1 ships id generators (Snowflake,
/// ULID, GuidV7, etc.) but does not include an idempotency cache; this store fills the
/// gap with a relational table guarded by the primary-key unique constraint, which gives
/// us atomic claim semantics for free.
/// </summary>
public sealed class OrionKeyIdempotencyStore : IIdempotencyStore
{
    private readonly BankingDbContext _db;
    private readonly IClock _clock;

    public OrionKeyIdempotencyStore(BankingDbContext db, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    public async Task<bool> TryClaimAsync(string key, string requestHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        var existing = await _db.Set<IdempotencyRecord>()
            .FirstOrDefaultAsync(r => r.Key == key, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // Re-using the same key with the same payload is fine (idempotent retry);
            // re-using it with a different payload is the replay attack we have to block.
            return string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal);
        }

        _db.Set<IdempotencyRecord>().Add(new IdempotencyRecord
        {
            Key = key,
            RequestHash = requestHash,
            ResponseJson = null,
            CreatedAtUtc = _clock.UtcNow.UtcDateTime,
            CompletedAtUtc = null,
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            // Another caller raced us to the insert. Re-read to decide if it's a same-payload
            // retry (claim succeeds for us too) or a payload collision (claim fails).
            _db.ChangeTracker.Clear();
            var raced = await _db.Set<IdempotencyRecord>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Key == key, cancellationToken)
                .ConfigureAwait(false);
            return raced is not null
                && string.Equals(raced.RequestHash, requestHash, StringComparison.Ordinal);
        }
    }

    public async Task<string?> GetCachedResponseAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var record = await _db.Set<IdempotencyRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key, cancellationToken)
            .ConfigureAwait(false);
        return record?.ResponseJson;
    }

    public async Task StoreResponseAsync(string key, string serialisedResponse, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(serialisedResponse);

        var record = await _db.Set<IdempotencyRecord>()
            .FirstOrDefaultAsync(r => r.Key == key, cancellationToken)
            .ConfigureAwait(false);
        if (record is null)
        {
            // StoreResponseAsync is only called after a successful TryClaimAsync, so a missing
            // row indicates a tracker reset or a concurrent purge. Re-create the row rather
            // than silently dropping the cached response.
            record = new IdempotencyRecord
            {
                Key = key,
                RequestHash = string.Empty,
                CreatedAtUtc = _clock.UtcNow.UtcDateTime,
            };
            _db.Set<IdempotencyRecord>().Add(record);
        }

        record.ResponseJson = serialisedResponse;
        record.CompletedAtUtc = _clock.UtcNow.UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
