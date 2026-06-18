namespace Moongazing.OrionShowcase.Api.ApiKeys;

using Microsoft.AspNetCore.Http;
using Moongazing.OrionLedger;
using Moongazing.OrionLedger.Keys;

/// <summary>
/// Authenticates partner/service callers presenting an <c>X-Api-Key</c> header by verifying it with
/// OrionLedger's <see cref="IApiKeyService"/>. A valid key's record is stashed on
/// <see cref="HttpContext.Items"/> under <see cref="VerificationItemKey"/> so downstream endpoint
/// filters can require it and inspect its scopes. The middleware does not reject requests that omit
/// the header: JWT-protected endpoints are unaffected, and partner endpoints enforce the key
/// themselves via <see cref="ApiKeyEndpointFilter"/>.
/// </summary>
public sealed class ApiKeyAuthMiddleware
{
    /// <summary>The request header carrying the API key.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>The <see cref="HttpContext.Items"/> key holding the successful verification.</summary>
    public const string VerificationItemKey = "OrionLedger.Verification";

    private readonly RequestDelegate _next;

    /// <summary>Create the middleware.</summary>
    /// <param name="next">The next delegate in the pipeline.</param>
    public ApiKeyAuthMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>Process a request.</summary>
    /// <param name="context">The request context.</param>
    /// <param name="apiKeys">The OrionLedger key service.</param>
    public async Task InvokeAsync(HttpContext context, IApiKeyService apiKeys)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(apiKeys);

        if (context.Request.Headers.TryGetValue(HeaderName, out var presented) &&
            !string.IsNullOrWhiteSpace(presented))
        {
            // Scope is verified per-endpoint by the filter, so we don't require one here.
            ApiKeyVerification verification = await apiKeys
                .VerifyAsync(presented.ToString(), requiredScope: null, context.RequestAborted)
                .ConfigureAwait(false);

            if (verification.IsValid)
            {
                context.Items[VerificationItemKey] = verification;
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}

/// <summary>Pipeline helper for <see cref="ApiKeyAuthMiddleware"/>.</summary>
public static class ApiKeyAuthMiddlewareExtensions
{
    /// <summary>Add the API-key authentication middleware to the pipeline.</summary>
    /// <param name="app">The application builder.</param>
    public static IApplicationBuilder UseBankingApiKeyAuth(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ApiKeyAuthMiddleware>();
    }
}
