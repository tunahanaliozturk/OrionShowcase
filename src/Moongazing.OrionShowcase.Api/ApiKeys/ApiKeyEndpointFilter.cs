namespace Moongazing.OrionShowcase.Api.ApiKeys;

using Microsoft.AspNetCore.Http;
using Moongazing.OrionLedger.Keys;

/// <summary>
/// Minimal-API endpoint filter that requires a valid OrionLedger API key holding a given scope. The
/// key is verified upstream by <see cref="ApiKeyAuthMiddleware"/>, which stashes the verification on
/// the request; this filter checks that it is present and carries the required scope, returning 401
/// when no valid key was presented and 403 when the key lacks the scope.
/// </summary>
public sealed class ApiKeyEndpointFilter : IEndpointFilter
{
    private readonly string _requiredScope;

    /// <summary>Create the filter for a required scope.</summary>
    /// <param name="requiredScope">The scope the presented key must hold.</param>
    public ApiKeyEndpointFilter(string requiredScope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredScope);
        _requiredScope = requiredScope;
    }

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.HttpContext.Items[ApiKeyAuthMiddleware.VerificationItemKey]
            is not ApiKeyVerification verification || !verification.IsValid)
        {
            return Results.Problem(
                detail: $"A valid '{ApiKeyAuthMiddleware.HeaderName}' is required.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (verification.Record?.Scopes.Contains(_requiredScope) != true)
        {
            return Results.Problem(
                detail: $"The API key does not hold the required scope '{_requiredScope}'.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context).ConfigureAwait(false);
    }
}

/// <summary>Convenience extensions for attaching <see cref="ApiKeyEndpointFilter"/>.</summary>
public static class ApiKeyEndpointFilterExtensions
{
    /// <summary>Require a valid API key holding the given scope.</summary>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="scope">The required scope.</param>
    public static TBuilder RequireApiKey<TBuilder>(this TBuilder builder, string scope)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddEndpointFilter(new ApiKeyEndpointFilter(scope));
        return builder;
    }
}
