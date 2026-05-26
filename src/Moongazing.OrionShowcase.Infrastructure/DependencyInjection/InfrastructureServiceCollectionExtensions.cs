namespace Moongazing.OrionShowcase.Infrastructure.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionPatch.DependencyInjection;
using Moongazing.OrionPatch.EntityFrameworkCore.DependencyInjection;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Infrastructure.Outbox;
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

        // OrionPatch core + EF Core storage backend bound to BankingDbContext.
        // Must run before AddDbContext so OrionPatchSaveChangesInterceptor is resolvable
        // inside UseOrionPatch(sp). UseEntityFrameworkCore registers IOutbox as scoped
        // (bound to the resolved BankingDbContext via ConditionalWeakTable).
        services.AddOrionPatch().UseEntityFrameworkCore<BankingDbContext>();

        // Domain-event-to-outbox bridge. Scoped because it depends on the scoped IOutbox.
        services.AddScoped<DomainEventOutboxAdapter>();

        services.AddDbContext<BankingDbContext>((sp, opt) =>
        {
            opt.UseNpgsql(cfg.GetConnectionString("Banking"));
            opt.UseOrionVault(sp);
            // Order matters: the domain-event adapter must enqueue events into IOutbox
            // BEFORE OrionPatch's interceptor drains the buffer into the change tracker
            // during SavingChanges. EF Core invokes interceptors in attachment order.
            opt.AddInterceptors(sp.GetRequiredService<DomainEventOutboxAdapter>());
            opt.UseOrionPatch(sp);
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
