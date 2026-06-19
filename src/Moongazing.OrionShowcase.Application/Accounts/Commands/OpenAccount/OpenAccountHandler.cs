namespace Moongazing.OrionShowcase.Application.Accounts.Commands.OpenAccount;

using MediatR;
using Moongazing.OrionGuard.Core;
using Moongazing.OrionShowcase.Application.Accounts.Sagas;
using Moongazing.OrionShowcase.Application.Common;

public sealed class OpenAccountHandler
    : IRequestHandler<OpenAccountCommand, Result<OpenAccountResult>>
{
    private readonly AccountOpeningSaga _saga;

    public OpenAccountHandler(AccountOpeningSaga saga)
    {
        _saga = saga;
    }

    public async Task<Result<OpenAccountResult>> Handle(
        OpenAccountCommand request,
        CancellationToken cancellationToken)
    {
        Ensure.NotNull(request);

        // Account opening runs as an OrionSaga: validate-customer -> create-account -> set-initial-limit,
        // each with a compensating action so a partial open is rolled back rather than left dangling.
        var context = new AccountOpeningContext(
            request.CustomerId,
            request.Iban,
            request.OpeningAmount,
            request.Currency,
            request.IdempotencyKey);

        var (result, ctx) = await _saga.RunAsync(context, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            // The saga rolled back the completed steps. A per-step timeout or a cancellation is a
            // TRANSIENT operational outcome (slow dependency, shutdown, client abort), NOT a business
            // rejection. OrionSaga 0.2 separates these via TimedOut/Cancelled.
            //
            // Throw a TransientOperationException for those so IdempotencyBehavior does NOT cache a
            // result for this key: the pipeline only stores a response when the handler returns
            // normally. A subsequent retry with the same idempotency key must be able to run the saga
            // again rather than replaying the cached transient failure.
            if (result.TimedOut)
            {
                throw new TransientOperationException(
                    $"Account opening timed out at step '{result.FailedStep}'. Please retry.");
            }

            if (result.Cancelled)
            {
                throw new TransientOperationException(
                    $"Account opening was cancelled at step '{result.FailedStep}'.");
            }

            // A genuine business failure IS a durable result: return it so the idempotency layer
            // captures it and a retry replays the same business decision rather than re-running.
            var reason = result.Failure?.Message ?? $"Account opening failed at step '{result.FailedStep}'.";
            return Result<OpenAccountResult>.Fail(reason);
        }

        return Result<OpenAccountResult>.Ok(new OpenAccountResult(ctx.AccountId!.Value.Value));
    }
}
