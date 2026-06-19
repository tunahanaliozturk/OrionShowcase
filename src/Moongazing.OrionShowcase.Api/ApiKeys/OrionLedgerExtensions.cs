namespace Moongazing.OrionShowcase.Api.ApiKeys;

using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionLedger;
using Moongazing.OrionLedger.Keys;
using Moongazing.OrionLedger.Storage;

/// <summary>
/// Registration of OrionLedger for partner/service API-key access. Seeds a single well-known demo
/// key into a process-local store so the partner endpoints can be exercised without an admin flow.
/// </summary>
public static class OrionLedgerExtensions
{
    /// <summary>The scope the seeded demo key carries and that the partner endpoint requires.</summary>
    public const string PartnerReadScope = "partner:read";

    /// <summary>
    /// The subject (key owner) the demo key is issued under. Bulk revocation operates per subject,
    /// so the seeded key carries one; rotation preserves it on the successor key.
    /// </summary>
    public const string DemoPartnerSubject = "partner:demo";

    /// <summary>
    /// A fixed demo API key for local/dev use. Production keys are issued via
    /// <see cref="IApiKeyService.IssueAsync"/> and never hard-coded; this constant exists only so the
    /// showcase has a presentable <c>X-Api-Key</c> value.
    /// </summary>
    public const string DemoApiKey = "ork_demo_partner_key_0001";

    /// <summary>
    /// Register OrionLedger (key service, options, diagnostics) over a pre-seeded in-memory store.
    /// The store is registered before <see cref="OrionLedgerServiceCollectionExtensions.AddOrionLedger"/>
    /// so it takes precedence over the default store, and is seeded with the demo key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddBankingApiKeys(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Pre-seed the demo key into a store registered ahead of AddOrionLedger so the package
        // binds to ours instead of adding its own empty in-memory store.
        var store = new InMemoryApiKeyStore();
        store.AddAsync(
            new ApiKeyRecord
            {
                Id = "demo-partner",
                Name = "Demo Partner",
                Subject = DemoPartnerSubject,
                DisplayPrefix = ApiKeyGenerator.DisplayPrefix(DemoApiKey),
                Hash = ApiKeyHasher.Hash(DemoApiKey),
                Scopes = new HashSet<string>(StringComparer.Ordinal) { PartnerReadScope },
                CreatedAt = DateTimeOffset.UtcNow,
            },
            CancellationToken.None).GetAwaiter().GetResult();

        services.AddSingleton<IApiKeyStore>(store);
        services.AddOrionLedger(o => o.Prefix = "ork_");

        return services;
    }
}
