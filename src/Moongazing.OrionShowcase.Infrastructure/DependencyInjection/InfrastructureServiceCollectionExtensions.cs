namespace Moongazing.OrionShowcase.Infrastructure.DependencyInjection;

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionAudit;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moongazing.OrionBeacon;
using Moongazing.OrionBeacon.Leasing;
using Moongazing.OrionBeacon.Observers;
using Moongazing.OrionLock;
using Moongazing.OrionLock.DependencyInjection;
using Moongazing.OrionLock.Postgres;
using Moongazing.OrionLock.Providers;
using Moongazing.OrionLock.Testing;
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
using Moongazing.OrionShowcase.Infrastructure.Limits;
using Moongazing.OrionShowcase.Infrastructure.Outbox;
using Moongazing.OrionShowcase.Infrastructure.Persistence;
using Moongazing.OrionShowcase.Infrastructure.Persistence.Repositories;
using Moongazing.OrionShowcase.Infrastructure.Time;
using Moongazing.OrionShowcase.Infrastructure.Vault;
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

            // Searchable encryption: register the HMAC key set backing the deterministic blind
            // index over the customer national id. The index key MUST be distinct from the
            // encryption key above (different secret material) so the blind index does not weaken
            // the randomized ciphertext. Opting in here makes IBlindIndexProvider resolvable.
            o.UseBlindIndex(k => k.Add(version: 1, base64Key: cfg["Vault:BlindIndexKey1"]!));
            o.ActiveBlindIndexVersion = 1;
        }).UseEntityFrameworkCore<BankingDbContext>();

        // Application port over the OrionVault blind index, consumed by the register-customer
        // handler (to stamp the index) and the customer repository (to look up by it).
        services.AddSingleton<INationalIdIndexer, OrionVaultNationalIdIndexer>();

        // OrionPatch core + EF Core storage backend bound to BankingDbContext.
        // Must run before AddDbContext so OrionPatchSaveChangesInterceptor is resolvable
        // inside UseOrionPatch(sp). UseEntityFrameworkCore registers IOutbox as scoped
        // (bound to the resolved BankingDbContext via ConditionalWeakTable).
        services.AddOrionPatch().UseEntityFrameworkCore<BankingDbContext>();

        // OrionPatch 0.3 dead-letter store + archival. UseEntityFrameworkCore registered the stock
        // EfCoreOutboxStorage as IOutboxStorage, which dead-letters in-place (Status = DeadLettered)
        // and has no archival. Replace it with the composite storage that ALSO implements
        // IDeadLetterStore + IOutboxArchivalStore: because the dispatcher routes an exhausted row to a
        // durable dead-letter store only when the injected IOutboxStorage implements IDeadLetterStore,
        // the IOutboxStorage registration itself must be the composite. Registered scoped (bound to the
        // scoped BankingDbContext) and re-exposed as the two capability interfaces for the archival
        // host and diagnostics.
        services.RemoveAll<Moongazing.OrionPatch.Abstractions.IOutboxStorage>();
        services.AddScoped<EfCoreOutboxDeadLetterArchivalStorage>();
        services.AddScoped<Moongazing.OrionPatch.Abstractions.IOutboxStorage>(
            sp => sp.GetRequiredService<EfCoreOutboxDeadLetterArchivalStorage>());
        services.AddScoped<Moongazing.OrionPatch.Abstractions.IDeadLetterStore>(
            sp => sp.GetRequiredService<EfCoreOutboxDeadLetterArchivalStorage>());
        services.AddScoped<Moongazing.OrionPatch.Abstractions.IOutboxArchivalStore>(
            sp => sp.GetRequiredService<EfCoreOutboxDeadLetterArchivalStorage>());

        // Drives archival: nothing in OrionPatch reaps processed rows, so the showcase supplies a host.
        var retentionDays = int.TryParse(cfg["Outbox:RetentionDays"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rd) ? rd : 7;
        var sweepMinutes = int.TryParse(cfg["Outbox:ArchiveSweepMinutes"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sm) ? sm : 60;
        services.AddSingleton(new OutboxArchivalOptions
        {
            Retention = TimeSpan.FromDays(retentionDays),
            SweepInterval = TimeSpan.FromMinutes(sweepMinutes),
        });
        services.AddHostedService<OutboxArchivalService>();

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
        // SystemClock satisfies both the domain IClock and Orion.Abstractions' IOrionClock so the
        // app and the Orion packages read wall-clock time from a single source.
        services.AddSingleton<SystemClock>();
        services.AddSingleton<IClock>(sp => sp.GetRequiredService<SystemClock>());
        services.AddSingleton<Moongazing.Orion.Abstractions.Time.IOrionClock>(sp => sp.GetRequiredService<SystemClock>());

        // OrionLock with Postgres pg_try_advisory_lock backend. This is the REAL cross-replica
        // safety mechanism: the exclusive-only IDistributedLock it registers serializes balance
        // MUTATIONS (deposit, withdraw, transfer) across every process/replica, because the advisory
        // lock lives in the shared Postgres instance. The money-mutating handlers depend on this
        // IDistributedLock, NOT on the in-memory reader-writer lock below, so a multi-replica
        // deployment still serializes mutations correctly.
        services.AddOrionLock().UsePostgres(bankingConnectionString);

        // OrionLock 0.4 reader-writer (shared/exclusive) lock, used to demonstrate SHARED-read vs
        // EXCLUSIVE semantics on the balance-read path (GetBalance takes a SHARED hold). It runs over
        // the canonical in-memory ISharedExclusiveLockProvider that ships in OrionLock.Testing, which
        // prevents writer starvation via a pending-writer reservation (a steady reader stream cannot
        // starve a waiting writer).
        //
        // IMPORTANT - SINGLE-PROCESS / SAMPLE-ONLY: this provider is in-memory, so its reader-writer
        // coordination spans only THIS process. It must NOT be used as the cross-replica safety
        // mechanism for money mutations - that role belongs to the distributed Postgres IDistributedLock
        // above. OrionLock 0.4 ships shared/exclusive semantics for the in-memory backend only; the
        // distributed backends do not yet model shared holders. In production a distributed
        // reader-writer provider (for example Redis) would back ISharedExclusiveLock and unify the two,
        // and the handlers - which depend only on these abstractions - would not change.
        services.AddSingleton<ISharedExclusiveLockProvider, InMemorySharedExclusiveLockProvider>();
        services.AddSingleton<ISharedExclusiveLock>(sp =>
            new SharedExclusiveLock(sp.GetRequiredService<ISharedExclusiveLockProvider>()));

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

        // In-memory daily-limit registry backing the OrionSaga set-initial-limit step.
        // Singleton so limit state survives across request scopes.
        services.AddSingleton<IAccountLimitRegistry, InMemoryAccountLimitRegistry>();

        // OrionBeacon leader election. The package's LeaderElectionService (an IHostedService it
        // registers) continuously acquires/renews a lease in the in-memory store; DailySettlementService
        // only does work while this node holds leadership, so the periodic job runs on a single replica.
        var beaconResource = cfg["Beacon:ResourceName"] ?? "orionshowcase:settlement";
        var leaseSeconds = int.TryParse(cfg["Beacon:LeaseSeconds"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ls) ? ls : 30;
        var renewSeconds = int.TryParse(cfg["Beacon:RenewSeconds"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rs) ? rs : 10;

        // Logs leadership transitions; resolved by AddOrionBeacon's LeaderElector factory.
        services.AddSingleton<ILeadershipObserver, LoggingLeadershipObserver>();

        // OrionBeacon 0.2 lease store with an injectable clock. AddOrionBeacon would otherwise
        // TryAdd a default InMemoryLeaseStore activated through its parameterless constructor (which
        // reads TimeProvider.System); registering the store explicitly here, BEFORE AddOrionBeacon,
        // binds the lease clock to the DI-provided TimeProvider so lease acquisition/renewal/expiry
        // read time from a single, testable source. The leasing and election calls already honor
        // cooperative cancellation: ILeaseStore.TryAcquireOrRenewAsync/ReleaseAsync accept the
        // stoppingToken threaded through LeaderElectionService and the elector.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ILeaseStore>(sp =>
            new InMemoryLeaseStore(sp.GetRequiredService<TimeProvider>()));

        services.AddOrionBeacon(o =>
        {
            o.ResourceName = beaconResource;
            o.CandidateId = $"{Environment.MachineName}:{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}";
            o.LeaseDuration = TimeSpan.FromSeconds(leaseSeconds);
            o.RenewInterval = TimeSpan.FromSeconds(renewSeconds);
        });

        // DailySettlementService added in Task 11; now gated on OrionBeacon leadership.
        services.AddScoped<RunDailySettlement>();
        services.AddHostedService<DailySettlementService>();

        return services;
    }
}
