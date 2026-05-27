namespace Moongazing.OrionShowcase.Application.Accounts.Commands.DepositMoney;

using MediatR;
using Moongazing.OrionGuard.Core;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class DepositMoneyHandler
    : IRequestHandler<DepositMoneyCommand, Result<DepositMoneyResult>>
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public DepositMoneyHandler(IAccountRepository accounts, IUnitOfWork uow, IClock clock)
    {
        _accounts = accounts;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<DepositMoneyResult>> Handle(
        DepositMoneyCommand request,
        CancellationToken cancellationToken)
    {
        Ensure.NotNull(request);

        var account = await _accounts.GetAsync(request.AccountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return Result<DepositMoneyResult>.Fail($"Account '{request.AccountId.Value}' was not found.");
        }

        var amount = new Money(request.Amount, request.Currency);
        try
        {
            account.Deposit(amount, request.IdempotencyKey, _clock);
        }
        catch (AccountNotActiveException ex)
        {
            return Result<DepositMoneyResult>.Fail(ex.Message);
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<DepositMoneyResult>.Ok(new DepositMoneyResult(account.Balance.Amount));
    }
}
