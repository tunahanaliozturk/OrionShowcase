namespace Moongazing.OrionShowcase.Api.Authorization;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moongazing.OrionGrant;

/// <summary>
/// Minimal-API endpoint filter that enforces a single OrionGrant permission on top of the standard
/// JWT authentication. It builds a <see cref="GrantPrincipal"/> from the authenticated user's role
/// claims and asks <see cref="IGrantAuthorizer"/> whether the required permission is held, returning
/// 403 with the failure reason when it is not.
/// </summary>
public sealed class PermissionEndpointFilter : IEndpointFilter
{
    private readonly string _requiredPermission;

    /// <summary>Create the filter for a required permission.</summary>
    /// <param name="requiredPermission">The permission the caller must hold.</param>
    public PermissionEndpointFilter(string requiredPermission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredPermission);
        _requiredPermission = requiredPermission;
    }

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var httpContext = context.HttpContext;
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var authorizer = httpContext.RequestServices.GetRequiredService<IGrantAuthorizer>();
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        var subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? "unknown";

        var principal = new GrantPrincipal { Subject = subject, Roles = roles };
        var result = authorizer.Authorize(principal, _requiredPermission);
        if (!result.IsGranted)
        {
            return Results.Problem(
                detail: result.FailureReason ?? $"Missing permission '{_requiredPermission}'.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context).ConfigureAwait(false);
    }
}

/// <summary>
/// Convenience extensions for attaching <see cref="PermissionEndpointFilter"/> to an endpoint.
/// </summary>
public static class PermissionEndpointFilterExtensions
{
    /// <summary>Require the caller to hold the given OrionGrant permission.</summary>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="permission">The required permission.</param>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddEndpointFilter(new PermissionEndpointFilter(permission));
        return builder;
    }
}
