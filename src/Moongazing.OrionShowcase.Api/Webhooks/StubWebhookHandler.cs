namespace Moongazing.OrionShowcase.Api.Webhooks;

using System.Net;
using Microsoft.Extensions.Logging;

/// <summary>
/// Primary HTTP handler attached to OrionRelay's <c>WebhookDispatcher</c> client when no real
/// partner receiver is configured. It logs the signed request (including the signature header
/// OrionRelay added) and returns <c>200 OK</c> so the showcase demonstrates the signing and
/// dispatch wiring without depending on an external endpoint.
/// </summary>
public sealed class StubWebhookHandler : HttpMessageHandler
{
    private readonly ILogger<StubWebhookHandler> _log;

    public StubWebhookHandler(ILogger<StubWebhookHandler> log) => _log = log;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var signature = request.Headers.TryGetValues("Orion-Signature", out var values)
            ? string.Join(',', values)
            : "(none)";

        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        _log.LogInformation(
            "OrionRelay stub transport accepted signed webhook to {Endpoint} (signature {Signature}): {Body}",
            request.RequestUri,
            signature,
            body);

        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
