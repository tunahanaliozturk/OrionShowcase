namespace Moongazing.OrionShowcase.Domain.Repositories;

using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public interface ICustomerRepository
{
    Task<Customer?> GetAsync(CustomerId id, CancellationToken cancellationToken);
    Task AddAsync(Customer customer, CancellationToken cancellationToken);

    /// <summary>
    /// Returns <c>true</c> when a customer already exists with the supplied national id.
    /// Backs the OrionGuard async uniqueness rule on customer registration: the national id is
    /// matched by its deterministic blind index, so the lookup resolves as an indexed equality
    /// seek and never decrypts a row.
    /// </summary>
    Task<bool> ExistsByNationalIdAsync(Tckn nationalId, CancellationToken cancellationToken);

    /// <summary>
    /// Finds a customer by exact national id using the OrionVault blind index. Demonstrates an
    /// equality lookup against searchable encryption: the probe is matched against every retained
    /// key version so rows written under an older index key are still found.
    /// </summary>
    Task<Customer?> FindByNationalIdAsync(Tckn nationalId, CancellationToken cancellationToken);
}
