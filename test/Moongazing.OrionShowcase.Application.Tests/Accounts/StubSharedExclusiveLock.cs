namespace Moongazing.OrionShowcase.Application.Tests.Accounts;

using System.Threading;
using System.Threading.Tasks;
using Moongazing.OrionLock;

/// <summary>
/// Test double for OrionLock 0.4's <see cref="ISharedExclusiveLock"/> that records the mode of each
/// acquired hold and always grants immediately, returning a handle whose disposal is tracked. Lets a
/// handler test assert it took a shared vs exclusive hold without standing up the real provider.
/// </summary>
internal sealed class StubSharedExclusiveLock : ISharedExclusiveLock
{
    public List<LockMode> AcquiredModes { get; } = new();

    public int Released { get; private set; }

    public Task<IDistributedLockHandle> AcquireExclusiveAsync(
        string key, DistributedLockOptions? options = null, CancellationToken cancellationToken = default) =>
        Acquire(LockMode.Exclusive);

    public Task<IDistributedLockHandle> AcquireSharedAsync(
        string key, DistributedLockOptions? options = null, CancellationToken cancellationToken = default) =>
        Acquire(LockMode.Shared);

    public Task<IDistributedLockHandle?> TryAcquireExclusiveAsync(
        string key, DistributedLockOptions? options = null, CancellationToken cancellationToken = default) =>
        TryAcquire(LockMode.Exclusive);

    public Task<IDistributedLockHandle?> TryAcquireSharedAsync(
        string key, DistributedLockOptions? options = null, CancellationToken cancellationToken = default) =>
        TryAcquire(LockMode.Shared);

    private Task<IDistributedLockHandle> Acquire(LockMode mode)
    {
        AcquiredModes.Add(mode);
        return Task.FromResult<IDistributedLockHandle>(new Handle(this));
    }

    private Task<IDistributedLockHandle?> TryAcquire(LockMode mode)
    {
        AcquiredModes.Add(mode);
        return Task.FromResult<IDistributedLockHandle?>(new Handle(this));
    }

    private sealed class Handle : IDistributedLockHandle
    {
        private readonly StubSharedExclusiveLock _owner;
        private bool _held = true;

        public Handle(StubSharedExclusiveLock owner) => _owner = owner;

        public string Key => "stub";
        public bool IsHeld => _held;
        public CancellationToken LostToken => CancellationToken.None;

        public ValueTask DisposeAsync()
        {
            if (_held)
            {
                _held = false;
                _owner.Released++;
            }
            return ValueTask.CompletedTask;
        }
    }
}
