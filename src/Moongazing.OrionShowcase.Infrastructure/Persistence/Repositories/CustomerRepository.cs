namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly BankingDbContext _db;
    private readonly INationalIdIndexer _nationalIdIndexer;

    public CustomerRepository(BankingDbContext db, INationalIdIndexer nationalIdIndexer)
    {
        _db = db;
        _nationalIdIndexer = nationalIdIndexer;
    }

    public Task<Customer?> GetAsync(CustomerId id, CancellationToken cancellationToken) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(customer);
        await _db.Customers.AddAsync(customer, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsByNationalIdAsync(Tckn nationalId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nationalId);

        // OrionVault blind index: compute the probe under every retained key version and match
        // against the indexed bytea column. Equality on the deterministic digest stands in for
        // equality on the (otherwise unsearchable) randomized ciphertext of the national id.
        var probes = _nationalIdIndexer.ComputeAllVersions(nationalId);
        var exists = await _db.Customers.AnyAsync(c => probes.Contains(c.NationalIdIndex), cancellationToken).ConfigureAwait(false);
        var total = await _db.Customers.CountAsync(cancellationToken).ConfigureAwait(false);
        var nullProbes = probes.Count(p => p is null);
#pragma warning disable CA1849
        Console.Error.WriteLine($"[DIAG-EXISTS] db={_db.Database.GetDbConnection().Database} probes={probes.Count} nullProbes={nullProbes} totalCustomers={total} exists={exists}");
#pragma warning restore CA1849
        return exists;
    }

    public Task<Customer?> FindByNationalIdAsync(Tckn nationalId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nationalId);

        var probes = _nationalIdIndexer.ComputeAllVersions(nationalId);
        return _db.Customers
            .FirstOrDefaultAsync(c => probes.Contains(c.NationalIdIndex), cancellationToken);
    }
}
