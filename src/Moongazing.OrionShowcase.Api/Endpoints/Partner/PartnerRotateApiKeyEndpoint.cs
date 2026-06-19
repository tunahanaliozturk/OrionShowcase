namespace Moongazing.OrionShowcase.Api.Endpoints.Partner;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionLedger;
using Moongazing.OrionLedger.Keys;
using Moongazing.OrionShowcase.Api.ApiKeys;
using Moongazing.OrionShowcase.Api.RateLimiting;

/// <summary>
/// Lets a partner rotate the API key it is currently authenticating with (OrionLedger 0.2
/// <c>RotateAsync</c>). A successor key is issued inheriting the predecessor's name, subject, scopes,
/// and expiry; within the grace window both the old and new keys verify, after which the old key
/// retires. The new plaintext key is returned once in the response and is never recoverable
/// afterwards. The caller is identified by the verified key stashed on the request by
/// <see cref="ApiKeyAuthMiddleware"/>, so a partner can only rotate its own key.
/// </summary>
internal static class PartnerRotateApiKeyEndpoint
{
    /// <summary>The default grace window during which the rotated-out key still verifies.</summary>
    private static readonly TimeSpan DefaultGrace = TimeSpan.FromMinutes(10);

    /// <summary>The upper bound accepted for a caller-supplied grace window.</summary>
    private static readonly TimeSpan MaxGrace = TimeSpan.FromDays(1);

    public static IEndpointConventionBuilder MapPartnerRotateApiKey(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapPost("/api/partner/api-key/rotate", Handle)
           .AllowAnonymous()
           .RequireApiKey(OrionLedgerExtensions.PartnerReadScope)
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyQuery)
           .WithName("PartnerRotateApiKey")
           .WithTags("Partner")
           .Produces<RotateApiKeyResponse>(200)
           .ProducesProblem(401)
           .ProducesProblem(403)
           .ProducesProblem(409);
    }

    private static async Task<IResult> Handle(
        HttpContext httpContext,
        IApiKeyService apiKeys,
        int? graceSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(apiKeys);

        // The middleware stashes the verification only when a valid key was presented, and the
        // RequireApiKey filter has already rejected the request otherwise, so Record is present here.
        if (httpContext.Items[ApiKeyAuthMiddleware.VerificationItemKey]
            is not ApiKeyVerification verification || verification.Record is null)
        {
            return Results.Problem(
                detail: "A valid API key is required to rotate.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var grace = graceSeconds is { } secs && secs >= 0
            ? TimeSpan.FromSeconds(Math.Min(secs, MaxGrace.TotalSeconds))
            : DefaultGrace;

        KeyRotation? rotation = await apiKeys
            .RotateAsync(verification.Record.Id, grace, cancellationToken)
            .ConfigureAwait(false);

        if (rotation is null)
        {
            // The key was revoked, expired, or already superseded between verification and rotation.
            return Results.Problem(
                detail: "The API key could not be rotated; it is no longer active.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var successor = rotation.Successor.Record;
        return Results.Ok(new RotateApiKeyResponse(
            successor.Id,
            rotation.Token,
            successor.DisplayPrefix,
            rotation.Predecessor.RetiresAt));
    }

    /// <summary>The result of a rotation: the new key shown once, plus when the old key retires.</summary>
    /// <param name="KeyId">The successor key's stable identifier.</param>
    /// <param name="ApiKey">The new plaintext key. Shown once; store it now.</param>
    /// <param name="DisplayPrefix">The successor's non-secret display prefix.</param>
    /// <param name="PreviousKeyRetiresAt">When the rotated-out key stops verifying, or null if already retired.</param>
    internal sealed record RotateApiKeyResponse(
        string KeyId,
        string ApiKey,
        string DisplayPrefix,
        DateTimeOffset? PreviousKeyRetiresAt);
}
