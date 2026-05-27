namespace Moongazing.OrionShowcase.Api.Endpoints.Accounts;

using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionShowcase.Api.Filters;
using Moongazing.OrionShowcase.Api.RateLimiting;
using Moongazing.OrionShowcase.Application.Accounts.Commands.WithdrawMoney;
using Moongazing.OrionShowcase.Domain.ValueObjects;

internal static class WithdrawEndpoint
{
    public static IEndpointConventionBuilder MapWithdraw(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapPost("/api/accounts/{id:guid}/withdraw", Handle)
           .RequireAuthorization()
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyTransfer)
           .WithName("WithdrawMoney")
           .WithTags("Accounts")
           .Produces<WithdrawResponse>(200)
           .ProducesValidationProblem()
           .ProducesProblem(409);
    }

    private static async Task<IResult> Handle(
        Guid id, WithdrawRequest req, IMediator mediator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(req);
        ArgumentNullException.ThrowIfNull(mediator);
        try
        {
            var result = await mediator.Send(new WithdrawMoneyCommand(
                new AccountId(id),
                req.Amount,
                Enum.Parse<Currency>(req.Currency),
                new IdempotencyKey(req.IdempotencyKey)), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.Ok(new WithdrawResponse(result.Value!.NewBalance))
                : Results.Problem(detail: result.Error, statusCode: 409);
        }
        catch (ValidationException ex) { return ValidationProblemFilter.Handle(ex); }
        catch (Exception ex) when (DomainExceptionFilter.TryHandle(ex) is { } r) { return r; }
    }

    internal sealed record WithdrawRequest(decimal Amount, string Currency, string IdempotencyKey);
    internal sealed record WithdrawResponse(decimal NewBalance);
}
