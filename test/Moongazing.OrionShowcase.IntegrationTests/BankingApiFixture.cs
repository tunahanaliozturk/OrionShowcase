namespace Moongazing.OrionShowcase.IntegrationTests;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class BankingApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("banking")
        .WithUsername("bank")
        .WithPassword("bank")
        .Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Banking"] = PostgresConnectionString,
                ["Vault:Key1"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
                ["Jwt:Issuer"] = "https://orionshowcase.local",
                ["Jwt:Audience"] = "orionshowcase-api",
                ["Jwt:SigningKey"] = "demo-signing-key-min-32-chars-for-hs256-algorithm",
                ["Otel:Endpoint"] = "http://localhost:4317",
                ["Seq:ServerUrl"] = "http://localhost:5341",
                ["OrionKey:WorkerId"] = "1",
                // Raise the per-policy fixed-window limits so tightly-packed integration
                // calls (login + register + open + transfer + balance reads, all from the
                // same client IP) cannot trip the production defaults.
                ["OrionGuard:Policies:login:Limit"] = "1000",
                ["OrionGuard:Policies:transfer:Limit"] = "1000",
                ["OrionGuard:Policies:query:Limit"] = "1000",
            });
        });

        return base.CreateHost(builder);
    }

    public async Task InitializeAsync() => await _postgres.StartAsync().ConfigureAwait(false);

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
