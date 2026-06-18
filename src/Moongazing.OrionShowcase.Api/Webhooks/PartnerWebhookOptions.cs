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
}
