namespace Moongazing.OrionShowcase.Api.Endpoints.Admin;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moongazing.OrionRelay.Delivery;
using Moongazing.OrionShowcase.Api.Authorization;
using Moongazing.OrionShowcase.Api.RateLimiting;

/// <summary>
/// Admin diagnostics over the OrionRelay in-memory dead-letter sink: lists partner webhook
/// deliveries that exhausted their retry budget (or hit a fatal non-retryable response) together
/// with their terminal failure context. Read-only and bounded by the sink's capacity. Guarded by
/// the <see cref="BankingPermissions.AdminKeysManage"/> permission. The sink is optional (it is
/// only registered when dead-letter capture is enabled), so the endpoint reports an empty list when
/// no sink is present.
/// </summary>
internal static class WebhookDeadLettersEndpoint
{
    public static IEndpointConventionBuilder MapWebhookDeadLetters(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapGet("/api/admin/webhooks/dead-letters", Handle)
           .RequireAuthorization()
           .RequirePermission(BankingPermissions.AdminKeysManage)
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyQuery)
           .WithName("AdminWebhookDeadLetters")
           .WithTags("Admin")
           .Produces<IReadOnlyList<WebhookDeadLetterDto>>(200);
    }

    internal static IResult Handle(IServiceProvider services)
    {
        // The in-memory sink is only registered when Relay:CaptureDeadLetters is enabled
        // (see AddPartnerWebhooks). Resolve it OPTIONALLY so this diagnostics endpoint degrades
        // gracefully when capture is disabled, rather than failing to resolve a required service.
        var sink = services.GetService<InMemoryDeadLetterSink>();
        if (sink is null)
        {
            // Capture disabled: no in-memory sink to read. Report an empty, well-formed list.
            return Results.Ok(Array.Empty<WebhookDeadLetterDto>());
        }

        var entries = sink.Entries
            .Select(e => new WebhookDeadLetterDto(
                e.Message.EventType,
                e.Message.EventId,
                e.Message.Endpoint.ToString(),
                e.Result.Attempts,
                e.Result.StatusCode,
                e.Result.FinalException?.GetType().Name,
                e.DeadLetteredAt))
            .ToArray();

        return Results.Ok(entries);
    }

    /// <summary>A single dead-lettered partner webhook delivery, projected for the admin view.</summary>
    internal sealed record WebhookDeadLetterDto(
        string? EventType,
        string? EventId,
        string Endpoint,
        int Attempts,
        int? LastStatusCode,
        string? FinalExceptionType,
        DateTimeOffset DeadLetteredAt);
}
