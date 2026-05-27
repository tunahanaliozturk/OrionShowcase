namespace Moongazing.OrionShowcase.Api.Endpoints.Accounts;

using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionShowcase.Api.Filters;
using Moongazing.OrionShowcase.Api.RateLimiting;
using Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountTransactions;
using Moongazing.OrionShowcase.Domain.ValueObjects;

internal static class GetTransactionsEndpoint
{
    public static IEndpointConventionBuilder MapGetTransactions(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapGet("/api/accounts/{id:guid}/transactions", Handle)
           .RequireAuthorization()
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyQuery)
           .WithName("GetAccountTransactions")
           .WithTags("Accounts")
           .Produces<IReadOnlyList<TransactionDto>>(200)
           .ProducesValidationProblem()
           .ProducesProblem(404);
    }

    private static async Task<IResult> Handle(
        Guid id, IMediator mediator, CancellationToken cancellationToken, int page = 1, int pageSize = 50)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        try
        {
            var result = await mediator.Send(
                new GetAccountTransactionsQuery(new AccountId(id), page, pageSize), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound();
        }
        catch (Exception ex) when (DomainExceptionFilter.TryHandle(ex) is { } r) { return r; }
    }
}
