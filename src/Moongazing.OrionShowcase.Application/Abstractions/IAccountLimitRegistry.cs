namespace Moongazing.OrionShowcase.Application.Abstractions;

using Moongazing.OrionShowcase.Domain.ValueObjects;

/// <summary>
/// Registers the per-account daily transfer limit assigned when an account is opened.
/// </summary>
/// <remarks>
/// Modelled as a side-effecting step in the account-opening saga so that a downstream
/// failure can compensate it: <see cref="SetDailyLimit"/> is undone by
/// <see cref="RemoveLimit"/>. A production system would back this with a limits service
/// or a persisted table; the in-process implementation keeps the showcase self-contained.
/// </remarks>
public interface IAccountLimitRegistry
{
    /// <summary>Assigns the initial daily transfer limit for <paramref name="accountId"/>.</summary>
    void SetDailyLimit(AccountId accountId, Money limit);

    /// <summary>Compensating action for <see cref="SetDailyLimit"/>; removes any limit set for the account.</summary>
    void RemoveLimit(AccountId accountId);

    /// <summary>Returns the configured daily limit, or <see langword="null"/> when none is set.</summary>
    Money? GetDailyLimit(AccountId accountId);
}
