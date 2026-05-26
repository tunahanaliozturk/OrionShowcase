namespace Moongazing.OrionShowcase.Domain.Repositories;

using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public interface ICustomerRepository
{
    Task<Customer?> GetAsync(CustomerId id, CancellationToken cancellationToken);
    Task AddAsync(Customer customer, CancellationToken cancellationToken);
}
