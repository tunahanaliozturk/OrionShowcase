namespace Moongazing.OrionShowcase.Application.Accounts;

using System.Globalization;
using Moongazing.OrionLock;
using Moongazing.OrionShowcase.Domain.ValueObjects;

/// <summary>
/// Shared keying and acquisition options for the per-account holds taken around account money
/// operations.
/// </summary>
/// <remarks>
/// <para>
/// Two lock layers share this key per account:
/// </para>
/// <list type="bullet">
///   <item><description>
///     Balance-mutating handlers (deposit, withdraw, transfer) acquire the DISTRIBUTED
///     <see cref="IDistributedLock"/> (Postgres advisory backend). This is the real cross-replica
///     safety mechanism: it serializes mutations of the same account across every process/replica.
///   </description></item>
///   <item><description>
///     The same handlers additionally take an EXCLUSIVE <see cref="ISharedExclusiveLock"/> hold, and
///     the balance read (GetBalance) takes a SHARED hold, over the in-memory reader-writer provider.
///     This demonstrates shared-read vs exclusive semantics and guards reads against half-applied
///     mutations WITHIN a single process. It is single-process/sample-only; a distributed
///     reader-writer provider would extend it across replicas in production.
///   </description></item>
/// </list>
/// <para>
/// Centralising the key format here guarantees every handler contends on the same key for a given
/// account at both layers.
/// </para>
/// </remarks>
internal static class AccountLock
{
    /// <summary>
    /// Per-acquisition lease/timeout options shared by every account hold. Kept identical to the
    /// pre-0.4 exclusive-lock settings so the acquisition and timeout semantics are unchanged.
    /// </summary>
    public static readonly DistributedLockOptions Options = new()
    {
        LeaseDuration = TimeSpan.FromSeconds(30),
        WaitTimeout = TimeSpan.FromSeconds(10),
        RetryInterval = TimeSpan.FromMilliseconds(250),
        AutoRenew = true,
    };

    /// <summary>Builds the per-account lock key both readers and writers contend on.</summary>
    public static string KeyFor(AccountId id) =>
        $"account:{id.Value.ToString("N", CultureInfo.InvariantCulture)}";
}
