namespace Moongazing.OrionShowcase.Api.Webhooks;

/// <summary>
/// Binds the <c>Relay</c> configuration section that controls the signed partner webhook
/// dispatched when a transfer completes.
/// </summary>
public sealed class PartnerWebhookOptions
{
    public const string SectionName = "Relay";

    /// <summary>Partner endpoint that receives the signed <c>transfer.completed</c> webhook.</summary>
    public string? Endpoint { get; set; }

    /// <summary>HMAC signing secret shared with the partner. A demo default is used when unset.</summary>
    public string SigningSecret { get; set; } = "orionshowcase-demo-webhook-secret";

    /// <summary>
    /// When <see langword="true"/> (or when <see cref="Endpoint"/> is unset), outbound calls are
    /// short-circuited by a stub HTTP handler so the app never fails on a missing partner.
    /// </summary>
    public bool UseStubTransport { get; set; }

    /// <summary>
    /// When <see langword="true"/>, an opt-in bounded in-memory dead-letter sink is registered so a
    /// webhook delivery that exhausts its retry budget (or hits a fatal non-retryable response) is
    /// captured with its final failure context instead of being lost. Defaults to
    /// <see langword="true"/> for the showcase so dead-lettered deliveries are observable; the
    /// OrionRelay default sink is a no-op. A durable sink fits production.
    /// </summary>
    public bool CaptureDeadLetters { get; set; } = true;

    /// <summary>
    /// The maximum number of dead-lettered deliveries retained by the in-memory sink before the
    /// oldest is evicted. Bounds the working set during a prolonged partner outage. Ignored when
    /// <see cref="CaptureDeadLetters"/> is <see langword="false"/>.
    /// </summary>
    public int DeadLetterCapacity { get; set; } = 256;
}
