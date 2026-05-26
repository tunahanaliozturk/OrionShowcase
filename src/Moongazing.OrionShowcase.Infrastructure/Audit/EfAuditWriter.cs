namespace Moongazing.OrionShowcase.Infrastructure.Audit;

using Moongazing.OrionKey;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Infrastructure.Persistence;

/// <summary>
/// EF-backed implementation of the Application-layer <see cref="IAuditWriter"/>. Each call
/// inserts a single <see cref="CommandAuditEntry"/> row with a fresh Snowflake id minted
/// through OrionKey. Lives next to the OrionAudit interceptor, which captures the
/// complementary entity-level diff stream on the same DbContext.
/// </summary>
public sealed class EfAuditWriter : IAuditWriter
{
    private readonly BankingDbContext _db;
    private readonly IClock _clock;

    public EfAuditWriter(BankingDbContext db, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    public async Task WriteAsync(
        string actor,
        string action,
        string requestJson,
        string? responseJson,
        bool succeeded,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(requestJson);

        var entry = new CommandAuditEntry
        {
            Id = OrionKey.NextSnowflake(),
            Actor = actor,
            Action = action,
            RequestJson = requestJson,
            ResponseJson = responseJson,
            Succeeded = succeeded,
            ErrorMessage = errorMessage,
            OccurredOnUtc = _clock.UtcNow.UtcDateTime,
        };

        _db.Set<CommandAuditEntry>().Add(entry);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
