namespace Moongazing.OrionShowcase.Api.Endpoints.Accounts;

using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionStream.Streaming;
using Moongazing.OrionShowcase.Api.Authorization;
using Moongazing.OrionShowcase.Api.Filters;
using Moongazing.OrionShowcase.Api.RateLimiting;
using Moongazing.OrionShowcase.Api.Streaming;
using Moongazing.OrionShowcase.Api.Webhooks;
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
           .RequirePermission(BankingPermissions.AccountsTransfer)
           .RequireRateLimiting(OrionGuardRateLimitingExtensions.PolicyTransfer)
           .WithName("TransferMoney")
           .WithTags("Accounts")
           .Produces<TransferResponse>(200)
           .ProducesValidationProblem()
           .ProducesProblem(403)
           .ProducesProblem(409);
    }

    private static async Task<IResult> Handle(
        Guid fromAccountId,
        TransferRequest req,
        IMediator mediator,
        PartnerWebhookNotifier webhooks,
        ISseHub activityHub,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(req);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(webhooks);
        ArgumentNullException.ThrowIfNull(activityHub);
        try
        {
            var result = await mediator.Send(new TransferMoneyCommand(
                new AccountId(fromAccountId),
                new AccountId(req.ToAccountId),
                new Money(req.Amount, Enum.Parse<Currency>(req.Currency)),
                new IdempotencyKey(req.IdempotencyKey)), cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return Results.Problem(detail: result.Error, statusCode: 409);
            }

            // OrionStream: publish the posted transfer to both accounts' activity topics so any
            // open SSE subscriber sees the balance movement in real time.
            AccountActivityPublisher.PublishTransferPosted(
                activityHub,
                result.Value!.TransferId,
                fromAccountId,
                req.ToAccountId,
                req.Amount,
                req.Currency,
                result.Value.NewSourceBalance);

            // OrionRelay: dispatch a signed transfer.completed webhook to the configured partner.
            // Best-effort and non-blocking on failure; the transfer is already committed.
            await webhooks.NotifyTransferCompletedAsync(
                result.Value.TransferId,
                fromAccountId,
                req.ToAccountId,
                req.Amount,
                req.Currency,
                cancellationToken).ConfigureAwait(false);

            return Results.Ok(new TransferResponse(result.Value.TransferId, result.Value.NewSourceBalance));
        }
        catch (Exception ex) when (DomainExceptionFilter.TryHandle(ex) is { } r) { return r; }
    }

    internal sealed record TransferRequest(Guid ToAccountId, decimal Amount, string Currency, string IdempotencyKey);
    internal sealed record TransferResponse(Guid TransferId, decimal NewSourceBalance);
}
