namespace Moongazing.OrionShowcase.Application.Tests.Idempotency;

using System.Text.Json;
using FluentAssertions;
using Moongazing.OrionOnce;
using Moongazing.OrionOnce.Storage;
using Xunit;

/// <summary>
/// Feature B: OrionOnce 0.2 idempotent money movement. Exercises the real
/// <see cref="IdempotentExecutor"/> over the same in-memory store and JSON codec the transfer
/// endpoint wires up, proving a repeated Idempotency-Key replays the captured result and does not
/// apply the transfer twice.
/// </summary>
public class TransferIdempotencyTests
{
    private sealed record TransferResult(Guid TransferId, decimal NewSourceBalance);

    private static readonly DelegateResultCodec<TransferResult> Codec = new(
        serialize: r => JsonSerializer.SerializeToUtf8Bytes(r),
        deserialize: payload => JsonSerializer.Deserialize<TransferResult>(payload)!,
        contentType: "application/json");

    private static IdempotentExecutor NewExecutor()
        => new(new InMemoryIdempotencyStore(TimeSpan.FromHours(1)));

    [Fact]
    public async Task Repeated_key_replays_the_captured_result_and_does_not_double_apply()
    {
        var executor = NewExecutor();
        const string key = "transfer-key-1";
        const string fingerprint = "acct-a|acct-b|100|EUR";
        var applied = 0;

        Task<TransferResult> Apply(CancellationToken ct)
        {
            applied++;
            return Task.FromResult(new TransferResult(Guid.NewGuid(), 900m));
        }

        var first = await executor.ExecuteAsync(key, fingerprint, Apply, Codec, CancellationToken.None);
        var second = await executor.ExecuteAsync(key, fingerprint, Apply, Codec, CancellationToken.None);

        applied.Should().Be(1, "the second call with the same key must replay, not re-run the transfer");
        second.Should().Be(first, "the replayed result must be byte-for-byte the first captured result");
        second.TransferId.Should().Be(first.TransferId);
    }

    [Fact]
    public async Task Different_key_applies_the_transfer_again()
    {
        var executor = NewExecutor();
        var applied = 0;

        Task<TransferResult> Apply(CancellationToken ct)
        {
            applied++;
            return Task.FromResult(new TransferResult(Guid.NewGuid(), 900m));
        }

        await executor.ExecuteAsync("key-1", "fp", Apply, Codec, CancellationToken.None);
        await executor.ExecuteAsync("key-2", "fp", Apply, Codec, CancellationToken.None);

        applied.Should().Be(2, "distinct idempotency keys are independent operations");
    }

    [Fact]
    public async Task Same_key_with_a_different_fingerprint_is_a_conflict()
    {
        var executor = NewExecutor();
        const string key = "transfer-key-2";

        await executor.ExecuteAsync(
            key, "acct-a|acct-b|100|EUR",
            _ => Task.FromResult(new TransferResult(Guid.NewGuid(), 900m)),
            Codec, CancellationToken.None);

        var act = async () => await executor.ExecuteAsync(
            key, "acct-a|acct-c|250|EUR",
            _ => Task.FromResult(new TransferResult(Guid.NewGuid(), 750m)),
            Codec, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<IdempotentExecutionException>();
        ex.Which.Outcome.Should().Be(IdempotencyOutcome.FingerprintMismatch);
    }

    [Fact]
    public async Task A_failed_transfer_is_not_captured_and_can_be_retried()
    {
        var executor = NewExecutor();
        const string key = "transfer-key-3";
        var attempts = 0;

        async Task<TransferResult> FailFirstThenSucceed(CancellationToken ct)
        {
            attempts++;
            await Task.Yield();
            return attempts == 1
                ? throw new InvalidOperationException("insufficient funds")
                : new TransferResult(Guid.NewGuid(), 0m);
        }

        var firstAttempt = async () => await executor.ExecuteAsync(
            key, "fp", FailFirstThenSucceed, Codec, CancellationToken.None);
        await firstAttempt.Should().ThrowAsync<InvalidOperationException>();

        // The key was released on failure, so the same key succeeds on retry.
        var retry = await executor.ExecuteAsync(key, "fp", FailFirstThenSucceed, Codec, CancellationToken.None);

        attempts.Should().Be(2);
        retry.Should().NotBeNull();
    }
}
