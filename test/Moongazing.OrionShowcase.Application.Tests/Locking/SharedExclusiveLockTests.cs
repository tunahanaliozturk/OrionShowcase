namespace Moongazing.OrionShowcase.Application.Tests.Locking;

using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moongazing.OrionLock;
using Moongazing.OrionLock.Testing;
using Xunit;

/// <summary>
/// Exercises OrionLock 0.4's reader-writer semantics over the canonical in-memory provider shipped in
/// OrionLock.Testing (the same backend the showcase wires behind <see cref="ISharedExclusiveLock"/>):
/// the coordination a balance read (shared) and a balance mutation (exclusive) rely on. Runs without
/// Docker; the provider is purely in-process. The provider prevents writer starvation via a
/// pending-writer reservation, asserted by <see cref="Waiting_writer_is_not_starved_by_a_continuous_reader_stream"/>.
/// </summary>
public class SharedExclusiveLockTests
{
    private const string Key = "account:11111111111111111111111111111111";

    private static readonly DistributedLockOptions FastOptions = new()
    {
        LeaseDuration = TimeSpan.FromSeconds(30),
        // Keep the non-blocking timeout short so the "is excluded" assertions fail fast rather than
        // spinning for the default 10 seconds.
        WaitTimeout = TimeSpan.FromMilliseconds(150),
        RetryInterval = TimeSpan.FromMilliseconds(10),
        AutoRenew = false,
    };

    private static SharedExclusiveLock NewLock() =>
        new(new InMemorySharedExclusiveLockProvider());

    [Fact]
    public async Task Two_shared_holds_coexist_on_the_same_key()
    {
        var sut = NewLock();

        var first = await sut.TryAcquireSharedAsync(Key, FastOptions, CancellationToken.None);
        var second = await sut.TryAcquireSharedAsync(Key, FastOptions, CancellationToken.None);

        first.Should().NotBeNull("a reader may hold the key");
        second.Should().NotBeNull("a second reader may hold the key concurrently with the first");

        await first!.DisposeAsync();
        await second!.DisposeAsync();
    }

    [Fact]
    public async Task Exclusive_hold_excludes_a_shared_hold()
    {
        var sut = NewLock();

        var writer = await sut.AcquireExclusiveAsync(Key, FastOptions, CancellationToken.None);

        // A reader cannot acquire while a writer owns the key.
        var reader = await sut.TryAcquireSharedAsync(Key, FastOptions, CancellationToken.None);
        reader.Should().BeNull("a writer holds the key exclusively");

        // Releasing the writer re-admits readers.
        await writer.DisposeAsync();
        var readerAfter = await sut.TryAcquireSharedAsync(Key, FastOptions, CancellationToken.None);
        readerAfter.Should().NotBeNull("the exclusive hold was released");
        await readerAfter!.DisposeAsync();
    }

    [Fact]
    public async Task Shared_hold_excludes_an_exclusive_hold()
    {
        var sut = NewLock();

        var reader = await sut.AcquireSharedAsync(Key, FastOptions, CancellationToken.None);

        // A writer cannot acquire while any reader owns the key.
        var writer = await sut.TryAcquireExclusiveAsync(Key, FastOptions, CancellationToken.None);
        writer.Should().BeNull("a reader holds the key");

        await reader.DisposeAsync();
        var writerAfter = await sut.TryAcquireExclusiveAsync(Key, FastOptions, CancellationToken.None);
        writerAfter.Should().NotBeNull("the shared hold was released");
        await writerAfter!.DisposeAsync();
    }

    [Fact]
    public async Task Exclusive_hold_excludes_another_exclusive_hold()
    {
        var sut = NewLock();

        var first = await sut.AcquireExclusiveAsync(Key, FastOptions, CancellationToken.None);

        var second = await sut.TryAcquireExclusiveAsync(Key, FastOptions, CancellationToken.None);
        second.Should().BeNull("only one exclusive holder is allowed");

        await first.DisposeAsync();
        var secondAfter = await sut.TryAcquireExclusiveAsync(Key, FastOptions, CancellationToken.None);
        secondAfter.Should().NotBeNull("the first exclusive hold was released");
        await secondAfter!.DisposeAsync();
    }

    [Fact]
    public async Task Waiting_writer_is_not_starved_by_a_continuous_reader_stream()
    {
        // This is the regression guard for the reader-preference starvation the hand-rolled provider
        // had: a steady stream of NEW readers must NOT be able to keep a waiting writer out forever.
        // The canonical provider records a pending-writer reservation when an exclusive acquire is
        // blocked by current readers, and refuses NEW shared acquires while that reservation is live,
        // so the existing readers drain and the writer eventually gets in.
        var sut = NewLock();

        // One reader currently holds the key.
        var reader = await sut.AcquireSharedAsync(Key, FastOptions, CancellationToken.None);

        // A writer tries to acquire and is blocked by the live reader. This non-blocking attempt fails
        // but, crucially, records the pending-writer reservation on the key.
        var blockedWriter = await sut.TryAcquireExclusiveAsync(Key, FastOptions, CancellationToken.None);
        blockedWriter.Should().BeNull("the writer cannot acquire while a reader holds the key");

        // Simulate the continuous reader stream: while the writer is waiting, NEW readers must be
        // refused. If readers kept being admitted here, the writer would starve - the original bug.
        for (var i = 0; i < 5; i++)
        {
            var newReader = await sut.TryAcquireSharedAsync(Key, FastOptions, CancellationToken.None);
            newReader.Should().BeNull(
                "a new reader must not be admitted while a writer is waiting, or the writer would starve");
        }

        // The existing reader drains.
        await reader.DisposeAsync();

        // With no readers left and the reservation honoured, the writer now acquires.
        var writer = await sut.TryAcquireExclusiveAsync(Key, FastOptions, CancellationToken.None);
        writer.Should().NotBeNull("once the last reader drained, the waiting writer acquires");
        await writer!.DisposeAsync();
    }

    [Fact]
    public async Task Holds_on_different_keys_do_not_interfere()
    {
        var sut = NewLock();

        var a = await sut.AcquireExclusiveAsync("account:aaa", FastOptions, CancellationToken.None);
        var b = await sut.TryAcquireExclusiveAsync("account:bbb", FastOptions, CancellationToken.None);

        b.Should().NotBeNull("an exclusive hold on one account does not block another account");

        await a.DisposeAsync();
        await b!.DisposeAsync();
    }
}
