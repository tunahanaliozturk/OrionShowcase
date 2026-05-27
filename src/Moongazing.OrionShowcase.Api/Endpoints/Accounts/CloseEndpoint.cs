namespace Moongazing.OrionShowcase.Api.Endpoints.Accounts;

using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionShowcase.Api.Filters;
using Moongazing.OrionShowcase.Api.RateLimiting;
using Moongazing.OrionShowcase.Application.Accounts.Commands.CloseAccount;
using Moongazing.OrionShowcase.Domain.ValueObjects;

internal static class CloseEndpoint
{
    public static IEndpointConventionBuilder MapClose(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapPost("/api/accounts/{id:guid}/close", Handle)
           .RequireAuthorization()
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyQuery)
           .WithName("CloseAccount")
           .WithTags("Accounts")
           .Produces(204)
           .ProducesValidationProblem()
           .ProducesProblem(409);
    }

    private static async Task<IResult> Handle(
        Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        try
        {
            var result = await mediator.Send(
                new CloseAccountCommand(new AccountId(id)), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.NoContent()
                : Results.Problem(detail: result.Error, statusCode: 409);
        }
        catch (Exception ex) when (DomainExceptionFilter.TryHandle(ex) is { } r) { return r; }
    }
}
