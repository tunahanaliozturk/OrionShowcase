namespace Moongazing.OrionShowcase.Api.Endpoints.Partner;

using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionShowcase.Api.ApiKeys;
using Moongazing.OrionShowcase.Api.Filters;
using Moongazing.OrionShowcase.Api.RateLimiting;
using Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountBalance;
using Moongazing.OrionShowcase.Domain.ValueObjects;

/// <summary>
/// A read-only balance lookup for partner/service callers authenticated by an OrionLedger API key
/// (<c>X-Api-Key</c>) rather than a JWT. It is the API-key counterpart of the JWT-protected
/// <c>GetAccountBalance</c> endpoint and requires the <c>partner:read</c> scope.
/// </summary>
internal static class PartnerBalanceEndpoint
{
    public static IEndpointConventionBuilder MapPartnerBalance(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapGet("/api/partner/accounts/{id:guid}/balance", Handle)
           .AllowAnonymous()
           .RequireApiKey(OrionLedgerExtensions.PartnerReadScope)
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyQuery)
           .WithName("PartnerGetAccountBalance")
           .WithTags("Partner")
           .Produces<AccountBalanceDto>(200)
           .ProducesProblem(401)
           .ProducesProblem(403)
           .ProducesProblem(404);
    }

    private static async Task<IResult> Handle(
        Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        try
        {
            var result = await mediator.Send(
                new GetAccountBalanceQuery(new AccountId(id)), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound();
        }
        catch (Exception ex) when (DomainExceptionFilter.TryHandle(ex) is { } r) { return r; }
    }
}
