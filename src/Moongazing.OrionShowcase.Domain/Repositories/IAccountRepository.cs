namespace Moongazing.OrionShowcase.Domain.Repositories;

using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public interface IAccountRepository
{
    Task<Account?> GetAsync(AccountId id, CancellationToken cancellationToken);
    Task AddAsync(Account account, CancellationToken cancellationToken);
}
