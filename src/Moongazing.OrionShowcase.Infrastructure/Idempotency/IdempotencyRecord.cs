namespace Moongazing.OrionShowcase.Infrastructure.Idempotency;

/// <summary>
/// EF-backed idempotency record. <see cref="Key"/> is supplied by the client (HTTP header
/// or message metadata); <see cref="RequestHash"/> guards against the classic "same key,
/// different payload" replay attack. <see cref="ResponseJson"/> caches the original
/// response so a retry returns byte-for-byte identical output.
/// </summary>
public sealed class IdempotencyRecord
{
    /// <summary>Client-supplied idempotency key (the primary key of this table).</summary>
    public string Key { get; set; } = default!;

    /// <summary>SHA-256 hex digest of the original request payload.</summary>
    public string RequestHash { get; set; } = default!;

    /// <summary>Cached serialised response, or null while the original call is still in flight.</summary>
    public string? ResponseJson { get; set; }

    /// <summary>UTC timestamp at which the key was first claimed.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>UTC timestamp at which the response was stored (null while in flight).</summary>
    public DateTime? CompletedAtUtc { get; set; }
}
