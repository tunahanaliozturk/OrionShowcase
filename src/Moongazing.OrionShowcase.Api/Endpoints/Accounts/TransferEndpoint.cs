namespace Moongazing.OrionShowcase.Api.Endpoints.Accounts;

using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Moongazing.OrionOnce;
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
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    // OrionOnce result codec: persists the typed transfer response so a replayed request returns
    // the exact same payload (same transfer id, same balance) the first call produced.
    private static readonly DelegateResultCodec<TransferResponse> ResponseCodec = new(
        serialize: r => JsonSerializer.SerializeToUtf8Bytes(r),
        deserialize: payload => JsonSerializer.Deserialize<TransferResponse>(payload)
            ?? throw new InvalidOperationException("Captured transfer response deserialised to null."),
        contentType: "application/json");

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
        HttpContext httpContext,
        IMediator mediator,
        IdempotentExecutor idempotency,
        PartnerWebhookNotifier webhooks,
        ISseHub activityHub,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(req);
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(webhooks);
        ArgumentNullException.ThrowIfNull(activityHub);

        try
        {
            var headerKey = httpContext.Request.Headers[IdempotencyKeyHeader].ToString();

            // No Idempotency-Key: run the transfer directly (back-compat for callers that do not
            // opt into replay). With a key: run through OrionOnce so a retried POST replays the
            // captured result instead of moving money a second time.
            if (string.IsNullOrWhiteSpace(headerKey))
            {
                var direct = await ExecuteTransferAsync(
                    fromAccountId, req, mediator, webhooks, activityHub, cancellationToken).ConfigureAwait(false);
                return Results.Ok(direct);
            }

            // Fingerprint binds the captured result to this exact request, so reusing a key with a
            // different transfer body is rejected as a mismatch rather than silently replayed.
            var fingerprint = RequestFingerprint.Compute(
                httpContext.Request.Method,
                $"/api/accounts/{fromAccountId:D}/transfer",
                Encoding.UTF8.GetBytes($"{req.ToAccountId:D}|{req.Amount}|{req.Currency}"));

            var response = await idempotency.ExecuteAsync(
                headerKey,
                fingerprint,
                ct => ExecuteTransferAsync(fromAccountId, req, mediator, webhooks, activityHub, ct),
                ResponseCodec,
                cancellationToken).ConfigureAwait(false);

            return Results.Ok(response);
        }
        catch (TransferFailedException ex)
        {
            // Domain-level transfer rejection (missing account, frozen, insufficient funds surfaced
            // as a Result failure). Mirror the original 409 contract; the key was released so the
            // caller may retry with corrected input.
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (IdempotentExecutionException ex)
        {
            // Key reused with a different body, or still in flight: surface a conflict rather than
            // applying a second, divergent transfer.
            return Results.Problem(
                detail: $"Idempotency conflict: {ex.Outcome}.",
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (Exception ex) when (DomainExceptionFilter.TryHandle(ex) is { } r) { return r; }
    }

    // Runs the transfer command and its post-commit side effects (activity stream + partner webhook),
    // returning the typed response that OrionOnce captures and later replays. A transfer failure
    // throws so the executor releases the key and the failure is never cached as a success.
    private static async Task<TransferResponse> ExecuteTransferAsync(
        Guid fromAccountId,
        TransferRequest req,
        IMediator mediator,
        PartnerWebhookNotifier webhooks,
        ISseHub activityHub,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new TransferMoneyCommand(
            new AccountId(fromAccountId),
            new AccountId(req.ToAccountId),
            new Money(req.Amount, Enum.Parse<Currency>(req.Currency)),
            new IdempotencyKey(req.IdempotencyKey)), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            // Throw so OrionOnce releases the key (failure is not captured for replay).
            throw new TransferFailedException(result.Error ?? "Transfer failed.");
        }

        // OrionStream: publish the posted transfer to both accounts' activity topics so any open
        // SSE subscriber sees the balance movement in real time.
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

        return new TransferResponse(result.Value.TransferId, result.Value.NewSourceBalance);
    }

    // Thrown to abort idempotent capture when the underlying transfer command fails. Mapped to a
    // 409 by the catch above so the caller still sees the domain error.
    private sealed class TransferFailedException : Exception
    {
        public TransferFailedException() { }

        public TransferFailedException(string message) : base(message) { }

        public TransferFailedException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    internal sealed record TransferRequest(Guid ToAccountId, decimal Amount, string Currency, string IdempotencyKey);
    internal sealed record TransferResponse(Guid TransferId, decimal NewSourceBalance);
}
