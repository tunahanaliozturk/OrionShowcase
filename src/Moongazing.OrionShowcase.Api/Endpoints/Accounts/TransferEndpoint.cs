namespace Moongazing.OrionShowcase.Api.Endpoints.Accounts;

using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionShowcase.Api.Filters;
using Moongazing.OrionShowcase.Api.RateLimiting;
using Moongazing.OrionShowcase.Application.Accounts.Commands.TransferMoney;
using Moongazing.OrionShowcase.Domain.ValueObjects;

internal static class TransferEndpoint
{
    public static IEndpointConventionBuilder MapTransfer(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        // CA1716: 'from' is a reserved word in VB.NET; route parameter name kept as `fromAccountId` to avoid the analyzer warning.
        return app.MapPost("/api/accounts/{fromAccountId:guid}/transfer", Handle)
           .RequireAuthorization()
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyTransfer)
           .WithName("TransferMoney")
           .WithTags("Accounts")
           .Produces<TransferResponse>(200)
           .ProducesValidationProblem()
           .ProducesProblem(409);
    }

    private static async Task<IResult> Handle(
        Guid fromAccountId, TransferRequest req, IMediator mediator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(req);
        ArgumentNullException.ThrowIfNull(mediator);
        try
        {
            var result = await mediator.Send(new TransferMoneyCommand(
                new AccountId(fromAccountId),
                new AccountId(req.ToAccountId),
                new Money(req.Amount, Enum.Parse<Currency>(req.Currency)),
                new IdempotencyKey(req.IdempotencyKey)), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.Ok(new TransferResponse(result.Value!.TransferId, result.Value.NewSourceBalance))
                : Results.Problem(detail: result.Error, statusCode: 409);
        }
        catch (ValidationException ex) { return ValidationProblemFilter.Handle(ex); }
        catch (Exception ex) when (DomainExceptionFilter.TryHandle(ex) is { } r) { return r; }
    }

    internal sealed record TransferRequest(Guid ToAccountId, decimal Amount, string Currency, string IdempotencyKey);
    internal sealed record TransferResponse(Guid TransferId, decimal NewSourceBalance);
}
