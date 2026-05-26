namespace Moongazing.OrionShowcase.Api.Endpoints.Accounts;

using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionShowcase.Api.Filters;
using Moongazing.OrionShowcase.Application.Accounts.Commands.DepositMoney;
using Moongazing.OrionShowcase.Domain.ValueObjects;

internal static class DepositEndpoint
{
    public static IEndpointConventionBuilder MapDeposit(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapPost("/api/accounts/{id:guid}/deposit", Handle)
           .RequireAuthorization()
           .WithName("DepositMoney")
           .WithTags("Accounts")
           .Produces<DepositResponse>(200)
           .ProducesValidationProblem()
           .ProducesProblem(409);
    }

    private static async Task<IResult> Handle(
        Guid id, DepositRequest req, IMediator mediator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(req);
        ArgumentNullException.ThrowIfNull(mediator);
        try
        {
            var result = await mediator.Send(new DepositMoneyCommand(
                new AccountId(id),
                req.Amount,
                Enum.Parse<Currency>(req.Currency),
                new IdempotencyKey(req.IdempotencyKey)), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.Ok(new DepositResponse(result.Value!.NewBalance))
                : Results.Problem(detail: result.Error, statusCode: 409);
        }
        catch (ValidationException ex) { return ValidationProblemFilter.Handle(ex); }
        catch (Exception ex) when (DomainExceptionFilter.TryHandle(ex) is { } r) { return r; }
    }

    internal sealed record DepositRequest(decimal Amount, string Currency, string IdempotencyKey);
    internal sealed record DepositResponse(decimal NewBalance);
}
