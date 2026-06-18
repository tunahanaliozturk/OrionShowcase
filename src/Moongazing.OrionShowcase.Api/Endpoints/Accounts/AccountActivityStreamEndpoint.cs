namespace Moongazing.OrionShowcase.Api.Endpoints.Accounts;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionStream.AspNetCore;
using Moongazing.OrionStream.Streaming;
using Moongazing.OrionShowcase.Api.Streaming;

internal static class AccountActivityStreamEndpoint
{
    // Hard cap on a single SSE connection so a forgotten browser tab cannot hold a slot forever.
    private static readonly TimeSpan MaxStreamLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    public static IEndpointConventionBuilder MapAccountActivityStream(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapGet("/api/accounts/{id:guid}/activity/stream", Handle)
            .RequireAuthorization()
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

        // Bound the stream: end when the client disconnects or the lifetime cap elapses.
        using var lifetime = new CancellationTokenSource(MaxStreamLifetime);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);

        // Subscribe before writing so events published during the handshake are not missed.
        using var subscription = hub.Subscribe(AccountActivityPublisher.TopicFor(id));

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
