namespace Moongazing.OrionShowcase.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionVault.DependencyInjection;
using Moongazing.OrionVault.EntityFrameworkCore.DependencyInjection;

/// <summary>
/// Used by <c>dotnet ef migrations</c> to build a <see cref="BankingDbContext"/>
/// without going through the full application DI graph. Avoids the chicken-and-egg
/// situation where startup migration code tries to hit a database that does not exist yet.
/// <para>
/// OrionVault is wired in here as well so the DESIGN-TIME model matches what the application
/// builds at runtime. OrionVault applies its value converters through an <c>IModelCustomizer</c>
/// (installed by <c>UseOrionVault</c>) that only runs when the encryption services are visible to
/// the context. Without it, <c>dotnet ef migrations</c> would see the encrypted properties as their
/// CLR types (<c>string</c> / value-converted <c>Tckn</c>) and scaffold <c>text</c> columns, whereas
/// at runtime OrionVault rewrites those properties to <c>byte[]</c> (a Postgres <c>bytea</c> column,
/// because the ciphertext is raw bytes). Registering OrionVault here makes the scaffolded schema and
/// the migration snapshot reflect the real <c>bytea</c> columns, so the encryption actually applies
/// and <c>has-pending-model-changes</c> stays clean.
/// </para>
/// </summary>
public sealed class DesignTimeBankingDbContextFactory : IDesignTimeDbContextFactory<BankingDbContext>
{
    // Demo-only key material, identical in shape to the runtime configuration (appsettings + the
    // integration-test fixture). These are throwaway, non-secret values used purely so the
    // design-time OrionVault model customizer can resolve an encryptor and rewrite the encrypted
    // properties to bytea; they never encrypt real data at design time (no rows are touched when
    // scaffolding a migration). The 32-byte AES key and the distinct HMAC blind-index key match the
    // demo keys in src/.../appsettings.json so the design-time and runtime models are byte-identical.
    private const string DemoEncryptionKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    private const string DemoBlindIndexKey = "QkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkE=";

    // Built once for the (short-lived) design-time tooling process and kept alive for its duration.
    // The OrionVault IModelCustomizer resolves the encryption services from this provider lazily,
    // while EF Core builds the model AFTER CreateDbContext returns, so the provider must outlive the
    // returned context; a process-lifetime singleton is the simplest way to satisfy that without
    // disposing it prematurely.
    private static readonly ServiceProvider VaultServices = BuildVaultServices();

    public BankingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BankingDbContext>()
            .UseNpgsql("Host=localhost;Database=banking_design;Username=design;Password=design");

        // UseOrionVault is declared on the non-generic DbContextOptionsBuilder, so chaining off its
        // return value would lose the BankingDbContext type argument. Apply it to the generic builder
        // (which the extension mutates in place) and then read the typed Options.
        optionsBuilder.UseOrionVault(VaultServices);

        return new BankingDbContext(optionsBuilder.Options);
    }

    private static ServiceProvider BuildVaultServices()
    {
        var services = new ServiceCollection();

        // Mirror the runtime AddInfrastructure registration: a single static encryption key plus the
        // distinct blind-index HMAC key, bound to BankingDbContext via UseEntityFrameworkCore so the
        // non-keyed EncryptionConfigurator / value-converter factory the model customizer needs are
        // resolvable.
        services.AddOrionVault(o =>
        {
            o.UseStaticKeys(k => k.Add(keyId: 1, base64Key: DemoEncryptionKey));
            o.ActiveKeyId = 1;
            o.UseBlindIndex(k => k.Add(version: 1, base64Key: DemoBlindIndexKey));
            o.ActiveBlindIndexVersion = 1;
        }).UseEntityFrameworkCore<BankingDbContext>();

        return services.BuildServiceProvider();
    }
}
