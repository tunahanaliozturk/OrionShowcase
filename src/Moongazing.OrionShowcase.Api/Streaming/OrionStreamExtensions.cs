namespace Moongazing.OrionShowcase.Api.Streaming;

using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionStream;

/// <summary>
/// Registers OrionStream (Server-Sent Events) for the account-activity stream endpoint.
/// </summary>
public static class OrionStreamExtensions
{
    // Per-topic replay window retained for resume. A reconnecting client that sends a
    // Last-Event-ID matching one of these retained events replays everything published after
    // it; an id older than this window (already evicted) falls back to a from-now stream.
    private const int ReplayBufferCapacity = 128;

    public static IServiceCollection AddAccountActivityStreaming(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // OrionStream 0.2: registers ISseHub with a bounded per-subscriber buffer and a bounded
        // per-topic replay buffer. The per-subscriber buffer applies backpressure (drops oldest)
        // so a slow client cannot grow memory without limit. The replay buffer is what makes
        // Last-Event-ID resume work: the hub retains the newest ReplayBufferCapacity events per
        // topic and, on a resuming Subscribe, drains the events after the client's cursor into the
        // subscription before live events flow.
        //
        // Sizing: replayed events count against the subscriber buffer like any other delivery, so
        // a full replay burst plus in-flight live events must fit. SubscriberCapacity is therefore
        // sized to cover the whole replay window with headroom for live events arriving during the
        // replay drain; otherwise the oldest replayed events would be dropped before the client
        // reads them.
        services.AddOrionStream(options =>
        {
            options.ReplayBufferCapacity = ReplayBufferCapacity;
            options.SubscriberCapacity = ReplayBufferCapacity * 2;
            options.HeartbeatInterval = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
