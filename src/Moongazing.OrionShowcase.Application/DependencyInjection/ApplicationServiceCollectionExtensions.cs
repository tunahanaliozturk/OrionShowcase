using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionGuard.DependencyInjection;
using Moongazing.OrionSaga;
using Moongazing.OrionShowcase.Application.Accounts.Sagas;
using Moongazing.OrionShowcase.Application.Pipeline;

namespace Moongazing.OrionShowcase.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var assembly = typeof(ApplicationServiceCollectionExtensions).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // Scan for OrionGuard IValidator<T> implementations and register each as scoped.
        // Replaces FluentValidation's AddValidatorsFromAssembly with the OrionGuard equivalent.
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IValidator<>))
                {
                    services.AddScoped(iface, type);
                }
            }
        }

        // OrionSaga: registers SagaDiagnostics so saga runs/steps/compensations are metered.
        // The account-opening workflow is modelled as a saga with per-step compensation.
        services.AddOrionSaga();
        services.AddScoped<AccountOpeningSaga>();

        // Pipeline order: outermost first
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));

        return services;
    }
}
