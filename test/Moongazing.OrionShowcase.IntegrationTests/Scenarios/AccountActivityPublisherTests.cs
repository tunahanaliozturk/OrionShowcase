namespace Moongazing.OrionShowcase.IntegrationTests.Scenarios;

using FluentAssertions;
using Moongazing.OrionStream.Streaming;
using Moongazing.OrionShowcase.Api.Streaming;
using Xunit;

/// <summary>
/// Verifies the account-activity publisher stamps each emitted event with a stable, resume-usable
/// wire id (the SSE <c>id:</c> field) that OrionStream 0.2 can match a reconnecting client's
/// Last-Event-ID against. Pure in-process; no Postgres or HTTP host needed.
/// </summary>
public class AccountActivityPublisherTests
{
    // Minimal ISseHub capturing what was published, so the test asserts on the emitted events
    // without standing up the real broadcast hub.
    private sealed class CapturingHub : ISseHub
    {
        public List<(string Topic, ServerSentEvent Event)> Published { get; } = new();

        public StreamSubscription Subscribe(string topic) => throw new NotSupportedException();

        public StreamSubscription Subscribe(string topic, string? lastEventId) => throw new NotSupportedException();

        public int Publish(string topic, ServerSentEvent evt)
        {
            Published.Add((topic, evt));
            return 1;
        }

        public int SubscriberCount(string topic) => 0;
    }

    [Fact]
    public void PublishTransferPosted_stamps_a_stable_resume_id_on_each_topic()
    {
        var hub = new CapturingHub();
        var transferId = Guid.NewGuid();
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();

        AccountActivityPublisher.PublishTransferPosted(
            hub, transferId, from, to, amount: 25m, currency: "TRY", newSourceBalance: 75m);

        hub.Published.Should().HaveCount(2);

        var debit = hub.Published.Single(p => p.Topic == AccountActivityPublisher.TopicFor(from)).Event;
        var credit = hub.Published.Single(p => p.Topic == AccountActivityPublisher.TopicFor(to)).Event;

        // Every event carries a non-empty id usable as a resume cursor (OrionStream renders Id as the
        // wire id and matches Last-Event-ID against it).
        debit.Id.Should().NotBeNullOrEmpty();
        credit.Id.Should().NotBeNullOrEmpty();

        // Ids are stable and bound to the transfer, and the two legs differ so a single account that
        // is both source and destination never sees two events with the same id on one topic.
        debit.Id.Should().Be($"{transferId:N}:debit");
        credit.Id.Should().Be($"{transferId:N}:credit");
        debit.Id.Should().NotBe(credit.Id);
    }

    [Fact]
    public void PublishTransferPosted_uses_the_activity_event_name()
    {
        var hub = new CapturingHub();

        AccountActivityPublisher.PublishTransferPosted(
            hub, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), amount: 1m, currency: "TRY", newSourceBalance: 0m);

        hub.Published.Should().OnlyContain(p => p.Event.EventName == "activity");
    }
}
