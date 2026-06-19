using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Application.Pipeline;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

namespace Moongazing.OrionShowcase.Application.Tests.Pipeline;

public class IdempotencyBehaviorTests
{
    public sealed record IdempotentRequest(string Payload, IdempotencyKey IdempotencyKey) : IIdempotentCommand;

    private sealed class FakeStore : IIdempotencyStore
    {
        public ConcurrentDictionary<string, string> Cached { get; } = new();
        public ConcurrentDictionary<string, string> Hashes { get; } = new();

        public Task<bool> TryClaimAsync(string key, string requestHash, CancellationToken cancellationToken)
        {
            // Mirrors OrionKeyIdempotencyStore: a fresh key is claimed; re-claiming an existing key
            // succeeds only when the payload hash matches (a same-payload retry), and fails on a hash
            // mismatch (a replay with a different body). A claimed-but-uncompleted key (no cached
            // response yet) therefore lets a same-payload retry re-execute - which is exactly what a
            // transient failure needs.
            if (Hashes.TryGetValue(key, out var existing))
            {
                return Task.FromResult(string.Equals(existing, requestHash, StringComparison.Ordinal));
            }

            return Task.FromResult(Hashes.TryAdd(key, requestHash));
        }

        public Task<string?> GetCachedResponseAsync(string key, CancellationToken cancellationToken)
            => Task.FromResult(Cached.TryGetValue(key, out var v) ? v : null);

        public Task StoreResponseAsync(string key, string serialisedResponse, CancellationToken cancellationToken)
        {
            Cached[key] = serialisedResponse;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task First_call_invokes_handler_and_stores_response()
    {
        var store = new FakeStore();
        var sut = new IdempotencyBehavior<IdempotentRequest, string>(store);
        var calls = 0;

        var result = await sut.Handle(
            new IdempotentRequest("x", new IdempotencyKey("k1")),
            () => { calls++; return Task.FromResult("response-1"); },
            default);

        calls.Should().Be(1);
        result.Should().Be("response-1");
        store.Cached["k1"].Should().Be(JsonSerializer.Serialize("response-1"));
    }

    [Fact]
    public async Task Second_call_with_same_key_returns_cached_without_calling_handler()
    {
        var store = new FakeStore();
        var sut = new IdempotencyBehavior<IdempotentRequest, string>(store);

        await sut.Handle(
            new IdempotentRequest("x", new IdempotencyKey("k1")),
            () => Task.FromResult("response-1"),
            default);

        var calls = 0;
        var result = await sut.Handle(
            new IdempotentRequest("x", new IdempotencyKey("k1")),
            () => { calls++; return Task.FromResult("response-2"); },
            default);

        calls.Should().Be(0);
        result.Should().Be("response-1");
    }

    [Fact]
    public async Task Transient_failure_is_not_cached_and_a_retry_re_executes()
    {
        // Regression guard for finding #3: a TRANSIENT outcome (e.g. an account-opening saga
        // TimedOut/Cancelled, surfaced by the handler as a TransientOperationException) must NOT be
        // stored by the idempotency layer. If it were cached, a retry with the same key would replay
        // the transient failure instead of actually re-running the operation.
        var store = new FakeStore();
        var sut = new IdempotencyBehavior<IdempotentRequest, string>(store);
        var request = new IdempotentRequest("x", new IdempotencyKey("k-transient"));

        // First attempt: the handler hits a transient timeout and throws.
        var firstAttempt = async () => await sut.Handle(
            request,
            () => throw new TransientOperationException("saga timed out"),
            default);

        await firstAttempt.Should().ThrowAsync<TransientOperationException>();

        // Nothing was cached for this key, so the transient outcome is not a final idempotent result.
        store.Cached.Should().NotContainKey("k-transient");

        // Retry with the same key: the handler runs AGAIN (it is not short-circuited by a cached
        // transient result) and this time produces a durable success, which IS cached.
        var calls = 0;
        var result = await sut.Handle(
            request,
            () => { calls++; return Task.FromResult("recovered"); },
            default);

        calls.Should().Be(1, "the retry must re-execute because the transient failure was not cached");
        result.Should().Be("recovered");
        store.Cached["k-transient"].Should().Be(JsonSerializer.Serialize("recovered"));
    }
}
