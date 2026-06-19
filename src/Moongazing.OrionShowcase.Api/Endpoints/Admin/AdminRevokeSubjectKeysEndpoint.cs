namespace Moongazing.OrionShowcase.Api.Endpoints.Admin;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionLedger;
using Moongazing.OrionShowcase.Api.Authorization;
using Moongazing.OrionShowcase.Api.RateLimiting;

/// <summary>
/// Lets an administrator revoke every active API key for a subject in one call (OrionLedger 0.2
/// <c>RevokeAllForSubjectAsync</c>), for example to off-board a partner or respond to a suspected
/// key compromise. Already-inactive keys (revoked, expired, or retired) are skipped; the response
/// reports how many keys were newly revoked. Guarded by JWT authentication and the
/// <see cref="BankingPermissions.AdminKeysManage"/> permission.
/// </summary>
internal static class AdminRevokeSubjectKeysEndpoint
{
    public static IEndpointConventionBuilder MapAdminRevokeSubjectKeys(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapPost("/api/admin/api-keys/revoke-subject", Handle)
           .RequireAuthorization()
           .RequirePermission(BankingPermissions.AdminKeysManage)
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyQuery)
           .WithName("AdminRevokeSubjectKeys")
           .WithTags("Admin")
           .Produces<RevokeSubjectResponse>(200)
           .ProducesValidationProblem()
           .ProducesProblem(403);
    }

    private static async Task<IResult> Handle(
        RevokeSubjectRequest request,
        IApiKeyService apiKeys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(apiKeys);

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["subject"] = ["A non-empty subject is required."],
            });
        }

        var revoked = await apiKeys
            .RevokeAllForSubjectAsync(request.Subject, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(new RevokeSubjectResponse(request.Subject, revoked));
    }

    /// <summary>The subject whose active keys should be revoked.</summary>
    /// <param name="Subject">The key owner (user, tenant, or service id).</param>
    internal sealed record RevokeSubjectRequest(string Subject);

    /// <summary>The outcome of a bulk revocation.</summary>
    /// <param name="Subject">The subject that was targeted.</param>
    /// <param name="RevokedCount">How many active keys were newly revoked (inactive keys are skipped).</param>
    internal sealed record RevokeSubjectResponse(string Subject, int RevokedCount);
}
