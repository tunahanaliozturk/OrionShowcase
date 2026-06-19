namespace Moongazing.OrionShowcase.Application.Tests.Locking;

using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moongazing.OrionLock;
using Moongazing.OrionShowcase.Infrastructure.Locking;
using Xunit;

/// <summary>
/// Exercises OrionLock 0.4's reader-writer semantics over the showcase's in-memory provider: the
/// same coordination a balance read (shared) and a balance mutation (exclusive) rely on. Runs
/// without Docker; the provider is purely in-process.
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
