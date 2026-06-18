namespace Moongazing.OrionShowcase.Api.Webhooks;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moongazing.OrionRelay.Delivery;

/// <summary>
/// Builds and dispatches the signed <c>transfer.completed</c> partner webhook through OrionRelay.
/// OrionRelay's <see cref="IWebhookDispatcher"/> signs the body (HMAC) and applies the retry policy;
/// this notifier only shapes the payload and hands it off.
/// </summary>
public sealed class PartnerWebhookNotifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWebhookDispatcher _dispatcher;
    private readonly PartnerWebhookOptions _options;
    private readonly ILogger<PartnerWebhookNotifier> _log;

    public PartnerWebhookNotifier(
        IWebhookDispatcher dispatcher,
        IOptions<PartnerWebhookOptions> options,
        ILogger<PartnerWebhookNotifier> log)
    {
        ArgumentNullException.ThrowIfNull(options);
        _dispatcher = dispatcher;
        _options = options.Value;
        _log = log;
    }

    /// <summary>
    /// Dispatches a signed webhook describing a completed transfer. Failures are swallowed and logged:
    /// a partner outage must never fail the originating transfer, which has already been committed.
    /// </summary>
    public async Task NotifyTransferCompletedAsync(
        Guid transferId,
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        // When stub transport is used the endpoint is irrelevant, but WebhookMessage requires a Uri.
        var endpoint = string.IsNullOrWhiteSpace(_options.Endpoint)
            ? "https://partner.invalid/webhooks/transfers"
            : _options.Endpoint;

        var payload = new
        {
            eventType = "transfer.completed",
            transferId,
            fromAccountId,
            toAccountId,
            amount,
            currency,
            occurredAt = DateTimeOffset.UtcNow,
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);

        var message = new WebhookMessage
        {
            Endpoint = new Uri(endpoint, UriKind.Absolute),
            Body = body,
            ContentType = "application/json",
            EventId = transferId.ToString("N"),
            EventType = "transfer.completed",
        };

        try
        {
            var result = await _dispatcher.DispatchAsync(message, cancellationToken).ConfigureAwait(false);
            _log.LogInformation(
                "OrionRelay dispatched transfer.completed webhook for {TransferId} (succeeded={Succeeded}, attempts={Attempts}).",
                transferId,
                result.Succeeded,
                result.Attempts);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _log.LogWarning(ex, "OrionRelay webhook dispatch for {TransferId} failed; transfer remains committed.", transferId);
        }
    }
}
