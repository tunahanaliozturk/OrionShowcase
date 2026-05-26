namespace Moongazing.OrionShowcase.Api.Authentication;

using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Moongazing.OrionShowcase.Application.Abstractions;

public sealed class ClaimsCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _ctx;
    public ClaimsCurrentUser(IHttpContextAccessor ctx) => _ctx = ctx;

    public string Id => _ctx.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "anonymous";
    public string Username => _ctx.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "anonymous";
    public bool IsAuthenticated => _ctx.HttpContext?.User.Identity?.IsAuthenticated == true;
}
