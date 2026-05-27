namespace Moongazing.OrionShowcase.Api.Endpoints.Auth;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionShowcase.Api.Authentication;
using Moongazing.OrionShowcase.Api.RateLimiting;

internal static class LoginEndpoint
{
    private static readonly string[] DemoRoles = new[] { "customer" };

    public static IEndpointConventionBuilder MapLogin(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapPost("/api/auth/login", Handle)
           .AllowAnonymous()
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyLogin)
           .WithName("Login")
           .WithTags("Auth");
    }

    private static IResult Handle(LoginRequest req, JwtIssuer issuer)
    {
        ArgumentNullException.ThrowIfNull(req);
        ArgumentNullException.ThrowIfNull(issuer);
        // Dev-grade: hardcoded demo:demo. Production would use OIDC or a real user store.
        if (!string.Equals(req.Username, "demo", StringComparison.Ordinal) ||
            !string.Equals(req.Password, "demo", StringComparison.Ordinal))
            return Results.Unauthorized();
        var token = issuer.Issue(
            userId: "00000000-0000-0000-0000-000000000001",
            username: "demo",
            roles: DemoRoles);
        return Results.Ok(new LoginResponse(token));
    }

    internal sealed record LoginRequest(string Username, string Password);
    internal sealed record LoginResponse(string AccessToken);
}
