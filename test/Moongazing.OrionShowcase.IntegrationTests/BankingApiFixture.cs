namespace Moongazing.OrionShowcase.IntegrationTests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class BankingApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    // When BANKING_TEST_POSTGRES is set (CI provides a services Postgres), the suite provisions a
    // unique database on that server per fixture instead of starting a Testcontainers container.
    // This lets the end-to-end tests run in CI without the Docker-in-Docker race that the
    // Testcontainers + GitHub services-postgres combination caused, while keeping the per-test-class
    // isolation Testcontainers gave (each IClassFixture gets its own fresh database). Locally, with
    // the variable unset, a throwaway Testcontainers Postgres is used exactly as before.
    private const string ServerEnvVar = "BANKING_TEST_POSTGRES";

    private readonly string? _serverConnectionString =
        Environment.GetEnvironmentVariable(ServerEnvVar);

    private readonly PostgreSqlContainer? _container;
    private readonly string? _provisionedDatabase;
    private string _connectionString = string.Empty;

    public BankingApiFixture()
    {
        Console.Error.WriteLine($"[DIAG-FIX] envSet={!string.IsNullOrWhiteSpace(_serverConnectionString)} value='{_serverConnectionString}'");
        if (string.IsNullOrWhiteSpace(_serverConnectionString))
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("banking")
                .WithUsername("bank")
                .WithPassword("bank")
                .Build();
        }
        else
        {
            // A unique database name per fixture instance preserves isolation when several test
            // classes run in parallel against the one shared CI server.
            _provisionedDatabase = "banking_" + Guid.NewGuid().ToString("N");
        }
    }

    public string PostgresConnectionString => _connectionString;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Banking"] = _connectionString,
                ["Vault:Key1"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
                // Demo-only HMAC key for the national-id blind index (distinct from the encryption key).
                ["Vault:BlindIndexKey1"] = "QkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkE=",
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureTestServices(services =>
        {
            // Remove every application background service from the test host, keeping only the
            // framework's own web-host service. The endpoint tests drive the stores directly and do
            // not need the timer-based workers (outbox archival, daily settlement); critically the
            // OrionBeacon leader-election hosted service blocks host startup while it acquires a
            // lease, which deadlocks WebApplicationFactory's synchronous host.Start(). Matching by
            // implementation-type name is not enough because some of these are registered through a
            // factory (null ImplementationType), so we keep only GenericWebHostService and drop the
            // rest.
            const string webHost = "Microsoft.AspNetCore.Hosting.GenericWebHostService";
            var background = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && d.ImplementationType?.FullName != webHost)
                .ToList();

            foreach (var descriptor in background)
            {
                services.Remove(descriptor);
            }
        });
    }

    public async Task InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync().ConfigureAwait(false);
            _connectionString = _container.GetConnectionString();
            return;
        }

        // CI path: create a fresh database on the supplied server and point the app at it. The app
        // applies its migrations on startup (Program.cs), so the new database gets the full schema.
        var appConnection = new NpgsqlConnectionStringBuilder(_serverConnectionString)
        {
            Database = _provisionedDatabase,
        };
        _connectionString = appConnection.ConnectionString;

        await using var admin = new NpgsqlConnection(_serverConnectionString);
        await admin.OpenAsync().ConfigureAwait(false);
        await using var create = admin.CreateCommand();
        // The database name is an internally generated "banking_<guid>" identifier, never user input,
        // and a DDL identifier cannot be parameterized, so the interpolation is safe here.
#pragma warning disable CA2100
        create.CommandText = $"CREATE DATABASE \"{_provisionedDatabase}\"";
#pragma warning restore CA2100
        await create.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public new async Task DisposeAsync()
    {
        // Shut the host (and its database connections, including hosted services) down first.
        await base.DisposeAsync().ConfigureAwait(false);

        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
            return;
        }

        if (_provisionedDatabase is null || string.IsNullOrWhiteSpace(_serverConnectionString))
        {
            return;
        }

        await using var admin = new NpgsqlConnection(_serverConnectionString);
        await admin.OpenAsync().ConfigureAwait(false);
        await using var drop = admin.CreateCommand();
        // FORCE (Postgres 13+) terminates any lingering backend before dropping the database. The
        // name is the same internally generated identifier created above, never user input.
#pragma warning disable CA2100
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{_provisionedDatabase}\" WITH (FORCE)";
#pragma warning restore CA2100
        await drop.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
