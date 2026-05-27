namespace Moongazing.OrionShowcase.Api.RateLimiting;

using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Wires per-endpoint rate-limit policies from the <c>OrionGuard:Policies</c> configuration
/// section. The OrionGuard.AspNetCore 6.4.2 package is a validation library and does not
/// itself provide rate limiting, so the showcase backs the documented policies with the
/// built-in <c>Microsoft.AspNetCore.RateLimiting</c> middleware while preserving the
/// configuration shape the README advertises.
/// </summary>
public static class OrionGuardRateLimitingExtensions
{
    public const string PolicyLogin = "login";
    public const string PolicyTransfer = "transfer";
    public const string PolicyQuery = "query";

    public static IServiceCollection AddOrionGuardRateLimiting(this IServiceCollection services, IConfiguration cfg)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(cfg);

        var section = cfg.GetSection("OrionGuard:Policies");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            AddFixedWindowPolicy(options, section, PolicyLogin, fallbackLimit: 5, fallbackWindow: TimeSpan.FromMinutes(1));
            AddFixedWindowPolicy(options, section, PolicyTransfer, fallbackLimit: 10, fallbackWindow: TimeSpan.FromMinutes(1));
            AddFixedWindowPolicy(options, section, PolicyQuery, fallbackLimit: 100, fallbackWindow: TimeSpan.FromMinutes(1));
        });

        return services;
    }

    private static void AddFixedWindowPolicy(
        RateLimiterOptions options,
        IConfigurationSection root,
        string policyName,
        int fallbackLimit,
        TimeSpan fallbackWindow)
    {
        var policySection = root.GetSection(policyName);
        var limit = policySection.GetValue<int?>("Limit") ?? fallbackLimit;
        var window = TryParseTimeSpan(policySection["Window"]) ?? fallbackWindow;

        options.AddPolicy(policyName, ctx =>
        {
            ArgumentNullException.ThrowIfNull(ctx);
            var partitionKey = ctx.User.Identity?.IsAuthenticated == true
                ? ctx.User.Identity!.Name ?? "anon"
                : ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";

            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true,
            });
        });
    }

    private static TimeSpan? TryParseTimeSpan(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}
