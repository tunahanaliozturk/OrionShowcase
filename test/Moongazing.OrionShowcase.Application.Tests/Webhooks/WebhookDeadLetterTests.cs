namespace Moongazing.OrionShowcase.Application.Tests.Webhooks;

using System.Net;
using FluentAssertions;
using Moongazing.OrionRelay.Delivery;
using Moongazing.OrionRelay.Diagnostics;
using Xunit;

/// <summary>
/// Upgrade 1: OrionRelay 0.2 dead-lettering for partner webhooks. Exercises the real
/// <see cref="WebhookDispatcher"/> over a stub transport that returns 5xx until the attempt budget is
/// exhausted, asserting the abandoned delivery lands in the opt-in bounded
/// <see cref="InMemoryDeadLetterSink"/> exactly once with its terminal failure context. This is the
/// same sink the showcase wires through <c>AddPartnerWebhooks</c>; the production default is the
/// no-op sink, and a durable sink fits production.
/// </summary>
public sealed class WebhookDeadLetterTests : IDisposable
{
    private readonly WebhookDiagnostics _diagnostics = new();
    private readonly List<IDisposable> _disposables = new();

    private static WebhookMessage NewMessage() => new()
    {
        Endpoint = new Uri("https://partner.invalid/webhooks/transfers", UriKind.Absolute),
        Body = "{\"eventType\":\"transfer.completed\"}"u8.ToArray(),
        ContentType = "application/json",
        EventId = "evt-1",
        EventType = "transfer.completed",
    };

    private static WebhookDeliveryOptions FastOptions(int maxAttempts) => new()
    {
        MaxAttempts = maxAttempts,
        BaseDelay = TimeSpan.Zero,
        MaxDelay = TimeSpan.Zero,
        RequestTimeout = TimeSpan.FromSeconds(5),
    };

    private WebhookDispatcher NewDispatcher(HttpMessageHandler handler, IDeadLetterSink sink, WebhookDeliveryOptions options)
    {
        var client = new HttpClient(handler);
        _disposables.Add(handler);
        _disposables.Add(client);
        return new WebhookDispatcher(client, options, _diagnostics, signer: null, observer: null, sink);
    }

    [Fact]
    public async Task A_delivery_that_exhausts_its_budget_is_dead_lettered_once_with_its_failure_context()
    {
        var handler = new SequencedHandler(Enumerable.Repeat(HttpStatusCode.InternalServerError, 8).ToArray());
        var sink = new InMemoryDeadLetterSink(capacity: 16);
        var dispatcher = NewDispatcher(handler, sink, FastOptions(3));

        var result = await dispatcher.DispatchAsync(NewMessage(), CancellationToken.None);

        result.Succeeded.Should().BeFalse("every attempt returned 5xx");
        result.Attempts.Should().Be(3, "the dispatcher stops once MaxAttempts is reached");

        sink.Count.Should().Be(1, "an exhausted delivery is dead-lettered exactly once");
        var entry = sink.Entries.Single();
        entry.Message.EventId.Should().Be("evt-1");
        entry.Result.Attempts.Should().Be(3);
        entry.Result.StatusCode.Should().Be(500, "the terminal failure context carries the last status observed");
        entry.DeadLetteredAt.Should().NotBe(default);
    }

    [Fact]
    public async Task A_fatal_non_retryable_status_dead_letters_after_a_single_attempt()
    {
        // 400 is not retryable, so the dispatcher abandons immediately rather than exhausting the budget.
        var handler = new SequencedHandler(HttpStatusCode.BadRequest);
        var sink = new InMemoryDeadLetterSink(capacity: 16);
        var dispatcher = NewDispatcher(handler, sink, FastOptions(3));

        var result = await dispatcher.DispatchAsync(NewMessage(), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Attempts.Should().Be(1, "a fatal status is not retried");
        sink.Count.Should().Be(1);
        sink.Entries.Single().Result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task A_delivery_that_eventually_succeeds_is_not_dead_lettered()
    {
        // 5xx, 5xx, then 200: succeeds on the third attempt, so nothing is dead-lettered.
        var handler = new SequencedHandler(
            HttpStatusCode.InternalServerError,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK);
        var sink = new InMemoryDeadLetterSink(capacity: 16);
        var dispatcher = NewDispatcher(handler, sink, FastOptions(3));

        var result = await dispatcher.DispatchAsync(NewMessage(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Attempts.Should().Be(3);
        sink.Count.Should().Be(0, "a delivery that succeeds within its budget is never dead-lettered");
    }

    public void Dispose()
    {
        foreach (var d in _disposables)
        {
            d.Dispose();
        }

        _diagnostics.Dispose();
    }

    /// <summary>
    /// A stub transport that returns a fixed sequence of status codes, repeating the last one once the
    /// sequence is exhausted. Stands in for a partner receiver that is failing.
    /// </summary>
    private sealed class SequencedHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode[] _statuses;
        private int _index;

        public SequencedHandler(params HttpStatusCode[] statuses) => _statuses = statuses;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var status = _statuses[Math.Min(_index, _statuses.Length - 1)];
            _index++;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
