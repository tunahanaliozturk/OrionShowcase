using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Moongazing.OrionShowcase.Application.Abstractions;
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
            => Task.FromResult(Hashes.TryAdd(key, requestHash));

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
}
