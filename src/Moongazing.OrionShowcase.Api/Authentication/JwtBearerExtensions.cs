namespace Moongazing.OrionShowcase.Api.Authentication;

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moongazing.OrionShowcase.Application.Abstractions;

public static class JwtBearerExtensions
{
    public static IServiceCollection AddJwtBearerAuth(this IServiceCollection services, IConfiguration cfg)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(cfg);
        var issuer = cfg["Jwt:Issuer"]!;
        var audience = cfg["Jwt:Audience"]!;
        var key = cfg["Jwt:SigningKey"]!;

        services.AddSingleton(new JwtIssuer(issuer, audience, key));
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, ClaimsCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        services.AddAuthorization();
        return services;
    }
}
