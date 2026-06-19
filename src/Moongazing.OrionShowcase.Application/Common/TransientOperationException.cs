namespace Moongazing.OrionShowcase.Application.Common;

/// <summary>
/// Signals a TRANSIENT operational outcome (a per-step timeout, a cancellation, a slow dependency)
/// that must NOT be cached as a final idempotent result. The idempotency pipeline only persists a
/// response when the handler returns normally; by throwing this exception the handler ensures a
/// transient outcome is never stored, so a subsequent retry with the same idempotency key can run
/// the operation again instead of replaying a stale transient failure.
/// </summary>
/// <remarks>
/// Distinct from a genuine business failure, which IS a durable result and is returned (not thrown)
/// so the idempotency layer captures it and a retry replays the same business decision.
/// </remarks>
public sealed class TransientOperationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="TransientOperationException"/> class.</summary>
    public TransientOperationException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TransientOperationException"/> class with a message.</summary>
    /// <param name="message">A description of the transient outcome.</param>
    public TransientOperationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TransientOperationException"/> class with a message and inner exception.</summary>
    /// <param name="message">A description of the transient outcome.</param>
    /// <param name="innerException">The cause of the transient outcome, if any.</param>
    public TransientOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
