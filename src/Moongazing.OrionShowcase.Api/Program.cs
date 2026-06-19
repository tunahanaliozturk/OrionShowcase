using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Moongazing.OrionGuard.AspNetCore.Extensions;
using Moongazing.OrionLens;
using Moongazing.OrionShowcase.Api.ApiKeys;
using Moongazing.OrionShowcase.Api.Authentication;
using Moongazing.OrionShowcase.Api.Authorization;
using Moongazing.OrionShowcase.Api.Endpoints;
using Moongazing.OrionShowcase.Api.Health;
using Moongazing.OrionShowcase.Api.Idempotency;
using Moongazing.OrionShowcase.Api.Observability;
using Moongazing.OrionShowcase.Api.RateLimiting;
using Moongazing.OrionShowcase.Api.Redaction;
using Moongazing.OrionShowcase.Api.Streaming;
using Moongazing.OrionShowcase.Api.Swagger;
using Moongazing.OrionShowcase.Api.Webhooks;
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
    .AddOrionShowcaseHealthChecks()
    // OrionLens: ambient correlation context + outbound propagation handler.
    .AddOrionLens()
    // OrionShade: PII/secret redaction (IRedactor) used at customer-data log sites.
    .AddBankingRedaction()
    // OrionGrant: banking permission set mapped from JWT roles.
    .AddBankingAuthorization()
    // OrionLedger: API-key issuance/verification for partner endpoints (in-memory store).
    .AddBankingApiKeys()
    // OrionOnce: explicit idempotent execution of money movements. The transfer endpoint runs the
    // transfer through an IdempotentExecutor keyed on the Idempotency-Key header and replays the
    // captured result on a retry with the same key, so a double-submitted transfer is applied once.
    // This is the targeted, result-replaying alternative to the generic HTTP middleware: it captures
    // the typed transfer response (including the generated transfer id) rather than a raw HTTP body.
    .AddTransferIdempotency()
    // OrionRelay: signed transfer.completed webhooks to a partner endpoint (stub transport when unset).
    .AddPartnerWebhooks(builder.Configuration)
    // OrionStream: SSE hub backing GET /api/accounts/{id}/activity/stream.
    .AddAccountActivityStreaming();

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
    // Surface the seeded OrionLedger demo API key so partner endpoints can be exercised locally.
    app.Logger.LogInformation(
        "OrionLedger demo API key (dev only): send header X-Api-Key: {DemoApiKey}",
        Moongazing.OrionShowcase.Api.ApiKeys.OrionLedgerExtensions.DemoApiKey);
}

// OrionLens FIRST: establish the ambient correlation context (mint/echo X-Correlation-ID,
// read baggage) before anything logs, authenticates, or calls downstream services.
app.UseOrionLens();

// OrionLedger API-key authentication path for partner/service callers. Runs before JWT auth so
// an X-Api-Key principal is established for endpoints that opt into it; JWT-protected endpoints
// are unaffected when no key is present.
app.UseBankingApiKeyAuth();

app.UseOrionGuardValidation();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// OrionOnce idempotency is applied at the transfer endpoint via the IdempotentExecutor (see
// AddTransferIdempotency / TransferEndpoint) rather than as generic HTTP middleware, so the
// captured/replayed value is the typed transfer result, not an opaque response body.
app.MapBankingEndpoints();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = h => h.Tags.Contains("ready") });

app.MapGet("/", () => "OrionShowcase API running. See /swagger.");
app.Run();

public partial class Program;
