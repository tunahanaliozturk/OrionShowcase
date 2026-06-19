namespace Moongazing.OrionShowcase.IntegrationTests.Scenarios;

using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionStream.Streaming;
using Moongazing.OrionShowcase.Api.Streaming;
using Xunit;

/// <summary>
/// End-to-end check of OrionStream 0.2 Last-Event-ID resume against the real hub the application
/// registers (with the configured replay buffer). A subscriber that resumes from the id of an event
/// it already saw replays only the events published after it, so a reconnecting client misses nothing.
/// Uses the application's configured services via the Postgres-backed fixture (CI runs this suite).
/// </summary>
public class SseResumeTests : IClassFixture<BankingApiFixture>
{
    private readonly BankingApiFixture _fx;

    public SseResumeTests(BankingApiFixture fx) => _fx = fx;

    [Fact]
    public async Task Resuming_from_a_seen_id_replays_only_the_events_after_it()
    {
        // Resolve the same singleton hub the SSE endpoint uses.
        var hub = _fx.Services.GetRequiredService<ISseHub>();

        var account = Guid.NewGuid();
        var topic = AccountActivityPublisher.TopicFor(account);

        // Publish three events with stable producer ids, as the publisher does on the wire.
        hub.Publish(topic, Event("e1"));
        hub.Publish(topic, Event("e2"));
        hub.Publish(topic, Event("e3"));

        // A client reconnects having last seen "e1": it must replay e2 and e3 only.
        using var subscription = hub.Subscribe(topic, "e1");
        var replayed = await DrainAsync(subscription, expected: 2);

        replayed.Should().Equal("e2", "e3");
    }

    [Fact]
    public async Task Resuming_from_an_unknown_id_starts_from_now_with_no_replay()
    {
        var hub = _fx.Services.GetRequiredService<ISseHub>();

        var account = Guid.NewGuid();
        var topic = AccountActivityPublisher.TopicFor(account);

        hub.Publish(topic, Event("a1"));
        hub.Publish(topic, Event("a2"));

        // An id the replay buffer never held: no backlog is replayed (from-now fallback).
        using var subscription = hub.Subscribe(topic, "does-not-exist");

        // A live event published after the subscribe is still delivered.
        hub.Publish(topic, Event("a3"));
        var seen = await DrainAsync(subscription, expected: 1);

        seen.Should().Equal("a3");
    }

    private static ServerSentEvent Event(string id) => new()
    {
        EventName = "activity",
        Id = id,
        Data = string.Format(CultureInfo.InvariantCulture, "{{\"id\":\"{0}\"}}", id),
    };

    // Reads up to 'expected' events with a bounded wait so a regression cannot hang the suite.
    private static async Task<List<string>> DrainAsync(StreamSubscription subscription, int expected)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var ids = new List<string>();
        try
        {
            while (ids.Count < expected && await subscription.Reader.WaitToReadAsync(cts.Token))
            {
                while (ids.Count < expected && subscription.Reader.TryRead(out var evt))
                {
                    if (evt.Id is not null)
                    {
                        ids.Add(evt.Id);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Bounded wait elapsed; return whatever arrived so the assertion reports the shortfall.
        }

        return ids;
    }
}
