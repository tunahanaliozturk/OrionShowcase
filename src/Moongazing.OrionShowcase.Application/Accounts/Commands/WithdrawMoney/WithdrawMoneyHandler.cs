namespace Moongazing.OrionShowcase.Application.Accounts.Commands.WithdrawMoney;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class WithdrawMoneyHandler
    : IRequestHandler<WithdrawMoneyCommand, Result<WithdrawMoneyResult>>
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public WithdrawMoneyHandler(IAccountRepository accounts, IUnitOfWork uow, IClock clock)
    {
        _accounts = accounts;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<WithdrawMoneyResult>> Handle(
        WithdrawMoneyCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var account = await _accounts.GetAsync(request.AccountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return Result<WithdrawMoneyResult>.Fail($"Account '{request.AccountId.Value}' was not found.");
        }

        var amount = new Money(request.Amount, request.Currency);
        try
        {
            account.Withdraw(amount, request.IdempotencyKey, _clock);
        }
        catch (InsufficientFundsException ex)
        {
            return Result<WithdrawMoneyResult>.Fail(ex.Message);
        }
        catch (AccountNotActiveException ex)
        {
            return Result<WithdrawMoneyResult>.Fail(ex.Message);
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<WithdrawMoneyResult>.Ok(new WithdrawMoneyResult(account.Balance.Amount));
    }
}
