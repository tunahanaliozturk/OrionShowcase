namespace Moongazing.OrionShowcase.Infrastructure.DependencyInjection;

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionAudit;
using Moongazing.OrionLock.DependencyInjection;
using Moongazing.OrionLock.Postgres;
using Moongazing.OrionPatch.DependencyInjection;
using Moongazing.OrionPatch.EntityFrameworkCore.DependencyInjection;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Application.Settlement;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Infrastructure.Audit;
using Moongazing.OrionShowcase.Infrastructure.HostedServices;
using Moongazing.OrionShowcase.Infrastructure.Idempotency;
using Moongazing.OrionShowcase.Infrastructure.Outbox;
using Moongazing.OrionShowcase.Infrastructure.Persistence;
using Moongazing.OrionShowcase.Infrastructure.Persistence.Repositories;
using Moongazing.OrionShowcase.Infrastructure.Time;
using Moongazing.OrionVault.DependencyInjection;
using Moongazing.OrionVault.EntityFrameworkCore.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    private static int orionKeyConfigured;

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(cfg);

        var bankingConnectionString = cfg.GetConnectionString("Banking")
            ?? throw new InvalidOperationException("ConnectionStrings:Banking is required.");

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

        // OrionAudit entity-level capture: writes a diff row per insert/update/delete on the
        // audited types into the OrionAudit_Log table within the same SaveChanges transaction.
        // Distinct from EfAuditWriter below, which records command-level audit at the
        // MediatR pipeline layer.
        services.AddOrionAudit<BankingDbContext>(o => o
            .Audit<Account>()
            .Audit<Customer>()
            .Audit<Moongazing.OrionShowcase.Domain.Accounts.Transaction>());

        // Domain-event-to-outbox bridge. Scoped because it depends on the scoped IOutbox.
        services.AddScoped<DomainEventOutboxAdapter>();

        services.AddDbContext<BankingDbContext>((sp, opt) =>
        {
            opt.UseNpgsql(bankingConnectionString);
            opt.UseOrionVault(sp);
            // Order matters: the domain-event adapter must enqueue events into IOutbox
            // BEFORE OrionPatch's interceptor drains the buffer into the change tracker
            // during SavingChanges. EF Core invokes interceptors in attachment order.
            opt.AddInterceptors(sp.GetRequiredService<DomainEventOutboxAdapter>());
            opt.UseOrionPatch(sp);
            opt.UseOrionAudit(sp);
        });

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IClock, SystemClock>();

        // OrionLock with Postgres pg_try_advisory_lock backend. Registered as singleton by
        // the package so TransferMoneyHandler's IDistributedLock dependency resolves
        // without further plumbing.
        services.AddOrionLock().UsePostgres(bankingConnectionString);

        // OrionKey is a process-global static; configure once even if AddInfrastructure
        // is called from multiple hosts (tests do this).
        if (Interlocked.Exchange(ref orionKeyConfigured, 1) == 0)
        {
            var workerIdRaw = cfg["OrionKey:WorkerId"];
            if (int.TryParse(workerIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var workerId))
            {
                Moongazing.OrionKey.OrionKey.Configure(o => o.SnowflakeWorkerId = workerId);
            }
            // Otherwise OrionKey falls back to ORIONKEY_WORKER_ID env var or machine-name hash.
        }

        // Bridges from the Application abstractions to the OrionKey + EF infrastructure.
        services.AddScoped<IAuditWriter, EfAuditWriter>();
        services.AddScoped<IIdempotencyStore, OrionKeyIdempotencyStore>();

        // DailySettlementService added in Task 11
        services.AddScoped<RunDailySettlement>();
        services.AddHostedService<DailySettlementService>();

        return services;
    }
}
