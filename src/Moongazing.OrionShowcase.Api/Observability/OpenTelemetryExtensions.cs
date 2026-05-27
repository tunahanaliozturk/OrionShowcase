namespace Moongazing.OrionShowcase.Api.Observability;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/// <summary>
/// Wires OpenTelemetry tracing + metrics for every Orion ActivitySource and Meter the
/// showcase touches, plus ASP.NET Core, EF Core and Npgsql instrumentation. Exports
/// over OTLP/gRPC to the endpoint configured under <c>Otel:Endpoint</c>.
/// </summary>
/// <remarks>
/// ActivitySource / Meter names were verified against each Orion package's diagnostics class:
/// <list type="bullet">
///   <item>OrionAudit  — "OrionAudit"             (src/Moongazing.OrionAudit/Telemetry/OrionAuditTelemetry.cs)</item>
///   <item>OrionGuard  — "Moongazing.OrionGuard"  (src/Moongazing.OrionGuard.OpenTelemetry/OrionGuardInstrumentation.cs)</item>
///   <item>OrionLock   — "Moongazing.OrionLock"   (src/Moongazing.OrionLock/Diagnostics/OrionLockDiagnostics.cs)</item>
///   <item>OrionKey    — "Moongazing.OrionKey"    (Meter only, no ActivitySource — src/Moongazing.OrionKey/Diagnostics/OrionKeyDiagnostics.cs)</item>
///   <item>OrionPatch  — "Moongazing.OrionPatch"  (ActivitySource only, no Meter — src/Moongazing.OrionPatch/Telemetry/OrionPatchDiagnostics.cs)</item>
///   <item>OrionVault  — "Moongazing.OrionVault"  (src/Moongazing.OrionVault/Diagnostics/OrionVaultDiagnostics.cs)</item>
/// </list>
/// </remarks>
public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddOpenTelemetryForOrion(this IServiceCollection services, IConfiguration cfg)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(cfg);

        var endpoint = cfg["Otel:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("Configuration key 'Otel:Endpoint' must be set to an OTLP endpoint URI.");
        }

        var otlp = new Uri(endpoint);

        services.AddOpenTelemetry()
            .WithTracing(t => t
                .AddSource("OrionAudit")
                .AddSource("Moongazing.OrionGuard")
                .AddSource("Moongazing.OrionLock")
                .AddSource("Moongazing.OrionPatch")
                .AddSource("Moongazing.OrionVault")
                .AddAspNetCoreInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddNpgsql()
                .AddOtlpExporter(o => o.Endpoint = otlp))
            .WithMetrics(m => m
                .AddMeter("OrionAudit")
                .AddMeter("Moongazing.OrionGuard")
                .AddMeter("Moongazing.OrionLock")
                .AddMeter("Moongazing.OrionKey")
                .AddMeter("Moongazing.OrionVault")
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = otlp));

        return services;
    }
}
