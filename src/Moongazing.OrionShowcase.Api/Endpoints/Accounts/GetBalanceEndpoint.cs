namespace Moongazing.OrionShowcase.Api.Endpoints.Accounts;

using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionShowcase.Api.Filters;
using Moongazing.OrionShowcase.Api.RateLimiting;
using Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountBalance;
using Moongazing.OrionShowcase.Domain.ValueObjects;

internal static class GetBalanceEndpoint
{
    public static IEndpointConventionBuilder MapGetBalance(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapGet("/api/accounts/{id:guid}/balance", Handle)
           .RequireAuthorization()
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyQuery)
           .WithName("GetAccountBalance")
           .WithTags("Accounts")
           .Produces<AccountBalanceDto>(200)
           .ProducesValidationProblem()
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
        catch (ValidationException ex) { return ValidationProblemFilter.Handle(ex); }
        catch (Exception ex) when (DomainExceptionFilter.TryHandle(ex) is { } r) { return r; }
    }
}
