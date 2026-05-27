namespace Moongazing.OrionShowcase.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Used by <c>dotnet ef migrations</c> to build a <see cref="BankingDbContext"/>
/// without going through the full application DI graph. Avoids the chicken-and-egg
/// situation where startup migration code tries to hit a database that does not exist yet.
/// </summary>
public sealed class DesignTimeBankingDbContextFactory : IDesignTimeDbContextFactory<BankingDbContext>
{
    public BankingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BankingDbContext>()
            .UseNpgsql("Host=localhost;Database=banking_design;Username=design;Password=design")
            .Options;
        return new BankingDbContext(options);
    }
}
