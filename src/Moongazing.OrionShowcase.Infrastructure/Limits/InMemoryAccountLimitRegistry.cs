namespace Moongazing.OrionShowcase.Infrastructure.Limits;

using System.Collections.Concurrent;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Domain.ValueObjects;

/// <summary>
/// Process-local implementation of <see cref="IAccountLimitRegistry"/> used by the
/// account-opening saga. A real deployment would persist limits or call a limits service;
/// a concurrent dictionary keeps the showcase free of extra infrastructure.
/// </summary>
public sealed class InMemoryAccountLimitRegistry : IAccountLimitRegistry
{
    private readonly ConcurrentDictionary<Guid, Money> _limits = new();

    public void SetDailyLimit(AccountId accountId, Money limit)
    {
        ArgumentNullException.ThrowIfNull(limit);
        _limits[accountId.Value] = limit;
    }

    public void RemoveLimit(AccountId accountId) => _limits.TryRemove(accountId.Value, out _);

    public Money? GetDailyLimit(AccountId accountId) =>
        _limits.TryGetValue(accountId.Value, out var limit) ? limit : null;
}
