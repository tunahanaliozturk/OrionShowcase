namespace Moongazing.OrionShowcase.Infrastructure.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Infrastructure.Persistence;
using Moongazing.OrionShowcase.Infrastructure.Persistence.Repositories;
using Moongazing.OrionShowcase.Infrastructure.Time;
using Moongazing.OrionVault.DependencyInjection;
using Moongazing.OrionVault.EntityFrameworkCore.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(cfg);

        // OrionVault must be registered BEFORE DbContext so UseOrionVault(sp) can resolve it
        services.AddOrionVault(o =>
        {
            o.UseStaticKeys(k => k.Add(keyId: 1, base64Key: cfg["Vault:Key1"]!));
            o.ActiveKeyId = 1;
        }).UseEntityFrameworkCore<BankingDbContext>();

        services.AddDbContext<BankingDbContext>((sp, opt) =>
        {
            opt.UseNpgsql(cfg.GetConnectionString("Banking"));
            opt.UseOrionVault(sp);
            // OrionPatch wiring added in Task 9
        });

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IClock, SystemClock>();

        // OrionLock + OrionAudit + OrionKey added in Task 10
        // DailySettlementService added in Task 11

        return services;
    }
}
