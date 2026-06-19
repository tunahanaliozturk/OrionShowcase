namespace Moongazing.OrionShowcase.Application.Tests.Leasing;

using FluentAssertions;
using Moongazing.OrionBeacon.Leasing;
using Xunit;

/// <summary>
/// Upgrade 4: OrionBeacon 0.2 InMemoryLeaseStore with an injectable <see cref="TimeProvider"/>. Drives
/// the lease clock from a controllable provider to assert that a lease expires deterministically once
/// the clock advances past its duration, and that the leasing calls honor cooperative cancellation.
/// This is the constructor the showcase now wires (the lease clock is bound to the DI TimeProvider).
/// </summary>
public class InMemoryLeaseStoreClockTests
{
    private const string Resource = "orionshowcase:settlement";

    [Fact]
    public async Task A_lease_expires_once_the_injected_clock_advances_past_its_duration()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var store = new InMemoryLeaseStore(clock);
        var lease = TimeSpan.FromSeconds(30);

        var first = await store.TryAcquireOrRenewAsync(Resource, "node-a", lease, CancellationToken.None);
        first.IsHeld.Should().BeTrue("the first candidate acquires the free lease");

        // A different candidate cannot take the lease while it is still held.
        var contested = await store.TryAcquireOrRenewAsync(Resource, "node-b", lease, CancellationToken.None);
        contested.IsHeld.Should().BeFalse("the lease is still held by node-a");

        // Advance the injected clock past the lease duration: the lease is now expired and acquirable.
        clock.Advance(TimeSpan.FromSeconds(31));

        var afterExpiry = await store.TryAcquireOrRenewAsync(Resource, "node-b", lease, CancellationToken.None);
        afterExpiry.IsHeld.Should().BeTrue("node-b acquires the lease after it expires on the injected clock");
        afterExpiry.HolderId.Should().Be("node-b");
    }

    [Fact]
    public async Task Leasing_calls_honor_cooperative_cancellation()
    {
        var store = new InMemoryLeaseStore(new MutableTimeProvider(DateTimeOffset.UnixEpoch));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var acquire = async () => await store.TryAcquireOrRenewAsync(Resource, "node-a", TimeSpan.FromSeconds(30), cts.Token);
        await acquire.Should().ThrowAsync<OperationCanceledException>("a cancelled token aborts the store operation");

        var release = async () => await store.ReleaseAsync(Resource, "node-a", cts.Token);
        await release.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>A minimal controllable <see cref="TimeProvider"/> for advancing the lease clock in tests.</summary>
    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public MutableTimeProvider(DateTimeOffset start) => _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
