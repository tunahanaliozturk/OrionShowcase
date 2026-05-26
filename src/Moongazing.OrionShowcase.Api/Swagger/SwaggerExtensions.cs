namespace Moongazing.OrionShowcase.Api.Swagger;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSwaggerGen(opt =>
        {
            opt.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Moongazing.OrionShowcase API",
                Version = "v1",
                Description = "Banking sample integrating all six Moongazing.Orion packages."
            });

            opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT obtained from POST /api/auth/login."
            });
            opt.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });
        return services;
    }
}
