namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class AccountRepository : IAccountRepository
{
    private readonly BankingDbContext _db;
    public AccountRepository(BankingDbContext db) => _db = db;

    public Task<Account?> GetAsync(AccountId id, CancellationToken cancellationToken) =>
        _db.Accounts.Include(a => a.Transactions).FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task AddAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        await _db.Accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
    }
}
