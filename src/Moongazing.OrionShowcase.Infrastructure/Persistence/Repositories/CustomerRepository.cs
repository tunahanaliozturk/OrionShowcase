namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly BankingDbContext _db;
    public CustomerRepository(BankingDbContext db) => _db = db;

    public Task<Customer?> GetAsync(CustomerId id, CancellationToken cancellationToken) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(customer);
        await _db.Customers.AddAsync(customer, cancellationToken).ConfigureAwait(false);
    }
}
