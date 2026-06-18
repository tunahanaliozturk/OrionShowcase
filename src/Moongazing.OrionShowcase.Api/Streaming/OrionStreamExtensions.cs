namespace Moongazing.OrionShowcase.Api.Streaming;

using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionStream;

/// <summary>
/// Registers OrionStream (Server-Sent Events) for the account-activity stream endpoint.
/// </summary>
public static class OrionStreamExtensions
{
    public static IServiceCollection AddAccountActivityStreaming(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // OrionStream: registers ISseHub. Bounded per-subscriber capacity so a slow client
        // applies backpressure (drops) instead of growing memory without limit.
        services.AddOrionStream(options =>
        {
            options.SubscriberCapacity = 64;
            options.HeartbeatInterval = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
