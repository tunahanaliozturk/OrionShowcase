namespace Moongazing.OrionShowcase.Api.Endpoints.Accounts;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionStream.AspNetCore;
using Moongazing.OrionStream.Streaming;
using Moongazing.OrionShowcase.Api.Authorization;
using Moongazing.OrionShowcase.Api.Streaming;

internal static class AccountActivityStreamEndpoint
{
    // Hard cap on a single SSE connection so a forgotten browser tab cannot hold a slot forever.
    private static readonly TimeSpan MaxStreamLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    public static IEndpointConventionBuilder MapAccountActivityStream(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        // Authenticated and gated behind the accounts:read permission via OrionGrant, matching the
        // rest of the account surface. Note: this sample uses a single operator identity (the demo
        // login is not bound to a specific customer), so it does not enforce per-account ownership.
        // A real multi-tenant deployment would additionally verify that the caller owns this account
        // (compare the account's customer id to the caller identity) and return 403 otherwise.
        return app.MapGet("/api/accounts/{id:guid}/activity/stream", Handle)
            .RequireAuthorization()
            .RequirePermission(BankingPermissions.AccountsRead)
            .WithName("StreamAccountActivity")
            .WithTags("Accounts")
            .Produces(200, contentType: "text/event-stream");
    }

    private static async Task Handle(Guid id, HttpContext context, ISseHub hub, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(hub);

        context.Response.Headers["Content-Type"] = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        // Resume contract (OrionStream 0.2): a browser EventSource that reconnects after a dropped
        // connection re-sends the id of the last event it saw in the standard "Last-Event-ID"
        // request header. We pass it to Subscribe(topic, lastEventId) so the hub replays the
        // account-activity events published after that cursor from its bounded per-topic replay
        // buffer before live events flow, and the client misses nothing across the gap. The cursor
        // is the wire id each event carries (see AccountActivityPublisher, which stamps a stable
        // per-event id). When the header is absent (first connect) or names a cursor the replay
        // buffer no longer holds (evicted), the hub starts the stream from now with no replay.
        var lastEventId = context.Request.Headers["Last-Event-ID"].ToString();

        // Bound the stream: end when the client disconnects or the lifetime cap elapses.
        using var lifetime = new CancellationTokenSource(MaxStreamLifetime);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);

        // Subscribe before writing so events published during the handshake are not missed. Passing
        // the Last-Event-ID resumes from the client's cursor; a null/empty value starts from now.
        using var subscription = hub.Subscribe(
            AccountActivityPublisher.TopicFor(id),
            string.IsNullOrEmpty(lastEventId) ? null : lastEventId);

        try
        {
            await context.Response
                .WriteStreamAsync(subscription, HeartbeatInterval, linked.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal termination: client disconnected or the lifetime cap elapsed.
        }
    }
}
