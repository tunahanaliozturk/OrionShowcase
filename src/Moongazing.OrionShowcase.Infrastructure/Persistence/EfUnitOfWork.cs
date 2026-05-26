namespace Moongazing.OrionShowcase.Infrastructure.Persistence;

using Moongazing.OrionShowcase.Domain.Repositories;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly BankingDbContext _db;
    public EfUnitOfWork(BankingDbContext db) => _db = db;
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}
