namespace Moongazing.OrionShowcase.Infrastructure.Outbox;

using Moongazing.OrionPatch.Models;

/// <summary>
/// EF-backed storage row for an outbox message that exhausted its delivery budget and was routed
/// out of the hot outbox into the dead-letter store (OrionPatch 0.3 <c>IDeadLetterStore</c>). It is
/// the durable, relational counterpart of the in-memory dead-letter store that ships with
/// OrionPatch.Testing: the enqueue-time columns are copied verbatim from the originating
/// <see cref="OutboxRow"/> so a dead-lettered message can be inspected or replayed without consulting
/// the (already-removed) source row.
/// </summary>
internal sealed class OutboxDeadLetterEntity
{
    /// <summary>Identity of the originating outbox row. Stable across the move; the primary key here.</summary>
    public Guid Id { get; set; }

    /// <summary>Logical message type name, copied from the source row.</summary>
    public string MessageType { get; set; } = default!;

    /// <summary>JSON payload, copied from the source row.</summary>
    public string Payload { get; set; } = default!;

    /// <summary>Optional JSON-serialized header map, copied from the source row.</summary>
    public string? HeadersJson { get; set; }

    /// <summary>Optional correlation id, copied from the source row.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>When the originating domain event occurred (UTC).</summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>When the source row was first written to the outbox (UTC).</summary>
    public DateTime EnqueuedAtUtc { get; set; }

    /// <summary>Total dispatch attempts the row accumulated before it was abandoned.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Truncated error text from the final dispatch attempt.</summary>
    public string FinalError { get; set; } = default!;

    /// <summary>UTC instant at which the message was routed into the dead-letter store.</summary>
    public DateTime DeadLetteredAtUtc { get; set; }

    /// <summary>Project this storage row to the OrionPatch model type returned to callers.</summary>
    public DeadLetteredMessage ToModel() => new()
    {
        Id = Id,
        MessageType = MessageType,
        Payload = Payload,
        HeadersJson = HeadersJson,
        CorrelationId = CorrelationId,
        OccurredAtUtc = OccurredAtUtc,
        EnqueuedAtUtc = EnqueuedAtUtc,
        AttemptCount = AttemptCount,
        FinalError = FinalError,
        DeadLetteredAtUtc = DeadLetteredAtUtc,
    };
}
