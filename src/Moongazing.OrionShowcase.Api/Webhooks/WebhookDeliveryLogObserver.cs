namespace Moongazing.OrionShowcase.Api.Webhooks;

using Microsoft.Extensions.Logging;
using Moongazing.OrionRelay.Delivery;
using Moongazing.OrionRelay.Observers;

/// <summary>
/// OrionRelay delivery observer that logs the terminal outcome when a partner webhook is abandoned
/// after exhausting its retry budget. The dead-lettered entry itself is captured by the registered
/// <see cref="IDeadLetterSink"/>; this observer adds a structured log line so an operator sees the
/// abandonment even when the sink is only inspected on demand. Observers must not throw: OrionRelay
/// swallows any fault raised here, but this implementation only logs.
/// </summary>
public sealed partial class WebhookDeliveryLogObserver : IWebhookDeliveryObserver
{
    private readonly ILogger<WebhookDeliveryLogObserver> _log;

    /// <summary>Create the observer.</summary>
    /// <param name="log">The logger sink.</param>
    public WebhookDeliveryLogObserver(ILogger<WebhookDeliveryLogObserver> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
    }

    /// <inheritdoc />
    public void OnAttempt(WebhookMessage message, int attempt, int? statusCode, Exception? exception)
    {
        // Per-attempt outcomes are already covered by OrionRelay's own metrics; nothing to add here.
    }

    /// <inheritdoc />
    public void OnExhausted(WebhookMessage message, WebhookDeliveryResult result)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(result);

        LogExhausted(
            message.EventType ?? "(none)",
            message.EventId ?? "(none)",
            message.Endpoint,
            result.Attempts,
            result.StatusCode);
    }

    [LoggerMessage(
        EventId = 200,
        Level = LogLevel.Warning,
        Message = "OrionRelay dead-lettered webhook {EventType} (event {EventId}) to {Endpoint} after {Attempts} attempts (last status {StatusCode}).")]
    partial void LogExhausted(string eventType, string eventId, Uri endpoint, int attempts, int? statusCode);
}
