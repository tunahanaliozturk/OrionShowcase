namespace Moongazing.OrionShowcase.Application.Tests.Accounts;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moongazing.OrionLock;

/// <summary>
/// Test double for OrionLock's <see cref="IDistributedLock"/> (the Postgres advisory-backed lock the
/// money-mutating handlers take as their real cross-replica safety mechanism). Records, in invocation
/// order, the keys acquired and released and always grants immediately. Lets a handler test assert it
/// took the distributed lock per account in deterministic (sorted) order without standing up Postgres.
/// </summary>
internal sealed class StubDistributedLock : IDistributedLock
{
    public List<string> Acquired { get; } = new();

    public List<string> Released { get; } = new();

    public Task<IDistributedLockHandle> AcquireAsync(
        string key, DistributedLockOptions? options = null, CancellationToken cancellationToken = default)
    {
        Acquired.Add(key);
        return Task.FromResult<IDistributedLockHandle>(new Handle(key, this));
    }

    public Task<IDistributedLockHandle?> TryAcquireAsync(
        string key, DistributedLockOptions? options = null, CancellationToken cancellationToken = default)
    {
        Acquired.Add(key);
        return Task.FromResult<IDistributedLockHandle?>(new Handle(key, this));
    }

    private sealed class Handle : IDistributedLockHandle
    {
        private readonly StubDistributedLock _owner;
        private bool _held = true;

        public Handle(string key, StubDistributedLock owner)
        {
            Key = key;
            _owner = owner;
        }

        public string Key { get; }
        public bool IsHeld => _held;
        public CancellationToken LostToken => CancellationToken.None;

        public ValueTask DisposeAsync()
        {
            if (_held)
            {
                _held = false;
                _owner.Released.Add(Key);
            }
            return ValueTask.CompletedTask;
        }
    }
}
