namespace Moongazing.OrionShowcase.Application.Accounts;

using System.Globalization;
using Moongazing.OrionLock;
using Moongazing.OrionShowcase.Domain.ValueObjects;

/// <summary>
/// Shared keying and acquisition options for the per-account reader-writer hold taken around
/// account money operations via OrionLock 0.4's <see cref="ISharedExclusiveLock"/>.
/// </summary>
/// <remarks>
/// Balance-mutating handlers (deposit, withdraw, transfer) acquire an EXCLUSIVE hold; the balance
/// read (GetBalance) acquires a SHARED hold. Centralising the key format here guarantees every
/// handler contends on the same key for a given account, so a read cannot run concurrently with a
/// mutation of the same account.
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
