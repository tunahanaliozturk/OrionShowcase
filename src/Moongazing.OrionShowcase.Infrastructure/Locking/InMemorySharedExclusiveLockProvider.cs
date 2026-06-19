namespace Moongazing.OrionShowcase.Infrastructure.Locking;

using System.Collections.Generic;
using Moongazing.OrionLock;
using Moongazing.OrionLock.Diagnostics;
using Moongazing.OrionLock.Providers;

/// <summary>
/// In-memory reader-writer lock primitive backing OrionLock 0.4's <see cref="ISharedExclusiveLock"/>
/// for the showcase. For a given key it admits any number of <see cref="LockMode.Shared"/> holders OR
/// a single <see cref="LockMode.Exclusive"/> holder, never both. The core OrionLock package composes the
/// blocking-acquire retry loop, lease renewal, and diagnostics on top of this single-attempt primitive,
/// exactly as it does for the exclusive-only provider.
/// </summary>
/// <remarks>
/// <para>
/// This is a process-local provider chosen because OrionLock 0.4 ships shared/exclusive semantics for
/// the in-memory backend only; the distributed backends (the Postgres advisory-lock provider this
/// sample wires for the exclusive-only <c>IDistributedLock</c>) do not yet model shared holders. A
/// production multi-node deployment would back <see cref="ISharedExclusiveLock"/> with a distributed
/// reader-writer provider (for example Redis) so reader-writer coordination spans processes; the
/// consuming handlers are written against the <see cref="ISharedExclusiveLock"/> abstraction and need
/// not change when that provider is swapped in.
/// </para>
/// <para>
/// Lease handling: <see cref="LeaseDurationIsTtl"/> is false. Holds live for as long as the caller keeps
/// the handle (until release), mirroring the session-scoped Postgres provider, so OrionLock's
/// lease-expiry diagnostics are not falsely tripped when a handler legitimately holds a hold longer than
/// the configured lease.
/// </para>
/// </remarks>
[BackendName("inmemory")]
public sealed class InMemorySharedExclusiveLockProvider : ISharedExclusiveLockProvider
{
    private readonly Dictionary<string, KeyState> _keys = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <inheritdoc />
    /// <remarks>
    /// False: a hold is released explicitly by the owner rather than expiring on a wall-clock TTL, so
    /// the lease-expired-before-release diagnostic is suppressed for this backend.
    /// </remarks>
    public bool LeaseDurationIsTtl => false;

    /// <inheritdoc />
    public Task<bool> TryAcquireAsync(
        string key,
        string ownerToken,
        LockMode mode,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(ownerToken);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_keys.TryGetValue(key, out var state))
            {
                state = new KeyState();
                _keys[key] = state;
            }

            if (mode == LockMode.Exclusive)
            {
                // A writer needs the key entirely to itself: no readers and no other writer.
                if (state.ExclusiveOwner is not null || state.SharedOwners.Count > 0)
                {
                    return Task.FromResult(false);
                }

                state.ExclusiveOwner = ownerToken;
                return Task.FromResult(true);
            }

            // A reader may join while no writer holds the key. Re-acquiring as the same owner is
            // idempotent so a retry that already succeeded does not double-count.
            if (state.ExclusiveOwner is not null)
            {
                return Task.FromResult(false);
            }

            state.SharedOwners.Add(ownerToken);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<bool> TryRenewAsync(
        string key,
        string ownerToken,
        LockMode mode,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(ownerToken);
        cancellationToken.ThrowIfCancellationRequested();

        // Holds do not expire on a TTL here, so renewal succeeds iff the owner still holds the key in
        // the requested mode. This keeps the watchdog quiet while the handle is alive.
        lock (_gate)
        {
            return Task.FromResult(StillHolds(key, ownerToken, mode));
        }
    }

    /// <inheritdoc />
    public Task ReleaseAsync(
        string key,
        string ownerToken,
        LockMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(ownerToken);

        lock (_gate)
        {
            if (!_keys.TryGetValue(key, out var state))
            {
                return Task.CompletedTask;
            }

            if (mode == LockMode.Exclusive)
            {
                if (string.Equals(state.ExclusiveOwner, ownerToken, StringComparison.Ordinal))
                {
                    state.ExclusiveOwner = null;
                }
            }
            else
            {
                state.SharedOwners.Remove(ownerToken);
            }

            // Reclaim the entry once no holder remains so the dictionary does not grow unbounded.
            if (state.ExclusiveOwner is null && state.SharedOwners.Count == 0)
            {
                _keys.Remove(key);
            }

            return Task.CompletedTask;
        }
    }

    private bool StillHolds(string key, string ownerToken, LockMode mode)
    {
        if (!_keys.TryGetValue(key, out var state))
        {
            return false;
        }

        return mode == LockMode.Exclusive
            ? string.Equals(state.ExclusiveOwner, ownerToken, StringComparison.Ordinal)
            : state.SharedOwners.Contains(ownerToken);
    }

    private sealed class KeyState
    {
        public string? ExclusiveOwner { get; set; }

        public HashSet<string> SharedOwners { get; } = new(StringComparer.Ordinal);
    }
}
