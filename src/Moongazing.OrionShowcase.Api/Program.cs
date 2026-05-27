using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Moongazing.OrionGuard.AspNetCore.Extensions;
using Moongazing.OrionShowcase.Api.Authentication;
using Moongazing.OrionShowcase.Api.Endpoints;
using Moongazing.OrionShowcase.Api.Health;
using Moongazing.OrionShowcase.Api.Observability;
using Moongazing.OrionShowcase.Api.RateLimiting;
using Moongazing.OrionShowcase.Api.Swagger;
using Moongazing.OrionShowcase.Application.DependencyInjection;
using Moongazing.OrionShowcase.Infrastructure.DependencyInjection;
using Moongazing.OrionShowcase.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.UseSerilogForOrion();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddJwtBearerAuth(builder.Configuration)
    .AddProblemDetails()
    .AddEndpointsApiExplorer()
    .AddSwagger()
    .AddOrionGuardAspNetCore()
    .AddOrionGuardRateLimiting(builder.Configuration)
    .AddOpenTelemetryForOrion(builder.Configuration)
    .AddOrionShowcaseHealthChecks();
// Endpoints added in Task 13

var app = builder.Build();

// Apply EF migrations on startup (banking DB will be created from EF model in Task 15 via docker)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseOrionGuardValidation();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapBankingEndpoints();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = h => h.Tags.Contains("ready") });

app.MapGet("/", () => "OrionShowcase API running. See /swagger.");
app.Run();

public partial class Program;
