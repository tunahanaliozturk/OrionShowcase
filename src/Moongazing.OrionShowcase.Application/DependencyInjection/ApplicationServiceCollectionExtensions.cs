using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionShowcase.Application.Pipeline;

namespace Moongazing.OrionShowcase.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var assembly = typeof(ApplicationServiceCollectionExtensions).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);

        // Pipeline order: outermost first
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));

        return services;
    }
}
