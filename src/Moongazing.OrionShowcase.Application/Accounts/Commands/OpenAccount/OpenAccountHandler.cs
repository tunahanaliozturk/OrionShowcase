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
            // The saga rolled back the completed steps. Surface the originating failure to the caller.
            var reason = result.Failure?.Message ?? $"Account opening failed at step '{result.FailedStep}'.";
            return Result<OpenAccountResult>.Fail(reason);
        }

        return Result<OpenAccountResult>.Ok(new OpenAccountResult(ctx.AccountId!.Value.Value));
    }
}
