namespace Moongazing.OrionShowcase.Infrastructure.Outbox;

using Moongazing.OrionPatch.Models;

/// <summary>
/// EF-backed storage row for a successfully dispatched outbox message that has crossed the
/// retention window and been moved out of the hot outbox into the archive (OrionPatch 0.3
/// <c>IOutboxArchivalStore</c>). Keeping processed rows out of the active outbox keeps the
/// claim-query working set small while preserving an audit/replay horizon. The columns mirror
/// <see cref="OutboxRow"/> so an archived row round-trips back to the model type.
/// </summary>
internal sealed class OutboxArchiveEntity
{
    /// <summary>Identity of the originating outbox row. The primary key here.</summary>
    public Guid Id { get; set; }

    /// <summary>Logical message type name.</summary>
    public string MessageType { get; set; } = default!;

    /// <summary>JSON payload.</summary>
    public string Payload { get; set; } = default!;

    /// <summary>Optional JSON-serialized header map.</summary>
    public string? HeadersJson { get; set; }

    /// <summary>Optional correlation id.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>When the originating domain event occurred (UTC).</summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>When the row was first written to the outbox (UTC).</summary>
    public DateTime EnqueuedAtUtc { get; set; }

    /// <summary>Total dispatch attempts the row accumulated.</summary>
    public int AttemptCount { get; set; }

    /// <summary>The error text of the last failed attempt, if any.</summary>
    public string? LastError { get; set; }

    /// <summary>When the row was successfully dispatched (UTC). The retention cutoff is measured against this.</summary>
    public DateTime? ProcessedAtUtc { get; set; }

    /// <summary>When the row was moved into the archive (UTC).</summary>
    public DateTime ArchivedAtUtc { get; set; }

    /// <summary>Project this archived row back to the OrionPatch model type returned to callers.</summary>
    public OutboxRow ToModel() => new()
    {
        Id = Id,
        MessageType = MessageType,
        Payload = Payload,
        HeadersJson = HeadersJson,
        CorrelationId = CorrelationId,
        OccurredAtUtc = OccurredAtUtc,
        EnqueuedAtUtc = EnqueuedAtUtc,
        Status = OutboxStatus.Processed,
        AttemptCount = AttemptCount,
        LastError = LastError,
        ProcessedAtUtc = ProcessedAtUtc,
    };
}
