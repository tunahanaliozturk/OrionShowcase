namespace Moongazing.OrionShowcase.Infrastructure.Audit;

/// <summary>
/// Persisted command-level audit record. Written by <see cref="EfAuditWriter"/> from the
/// MediatR <c>AuditBehavior</c> pipeline for every <c>IAuditableCommand</c>.
/// </summary>
/// <remarks>
/// Complements OrionAudit, which captures entity-level INSERT/UPDATE/DELETE diffs at the
/// DbContext layer. This table records the higher-level "user X invoked command Y with
/// payload P, succeeded/failed at time T" view that operators want when investigating an
/// incident or reconstructing a customer's journey.
/// </remarks>
public sealed class CommandAuditEntry
{
    /// <summary>Snowflake id assigned at write time.</summary>
    public long Id { get; set; }

    /// <summary>Authenticated principal that invoked the command, or "anonymous".</summary>
    public string Actor { get; set; } = default!;

    /// <summary>Short command name (the request type name).</summary>
    public string Action { get; set; } = default!;

    /// <summary>Serialised request payload.</summary>
    public string RequestJson { get; set; } = default!;

    /// <summary>Serialised response, or null when the command threw.</summary>
    public string? ResponseJson { get; set; }

    /// <summary>True when the command completed without throwing.</summary>
    public bool Succeeded { get; set; }

    /// <summary>Exception message when <see cref="Succeeded"/> is false.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>UTC timestamp at which the entry was recorded.</summary>
    public DateTime OccurredOnUtc { get; set; }
}
