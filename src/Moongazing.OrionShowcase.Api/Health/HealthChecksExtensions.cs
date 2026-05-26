namespace Moongazing.OrionShowcase.Api.Health;

using Microsoft.Extensions.DependencyInjection;

public static class HealthChecksExtensions
{
    public static IServiceCollection AddOrionShowcaseHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHealthChecks();
        return services;
    }
}
