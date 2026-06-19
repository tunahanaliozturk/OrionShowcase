namespace Moongazing.OrionShowcase.Application.Accounts.Commands.DepositMoney;

using MediatR;
using Moongazing.OrionGuard.Core;
using Moongazing.OrionLock;
using Moongazing.OrionShowcase.Application.Accounts;
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
    private readonly ISharedExclusiveLock _locker;
    private readonly IClock _clock;

    public DepositMoneyHandler(
        IAccountRepository accounts,
        IUnitOfWork uow,
        ISharedExclusiveLock locker,
        IClock clock)
    {
        _accounts = accounts;
        _uow = uow;
        _locker = locker;
        _clock = clock;
    }

    public async Task<Result<DepositMoneyResult>> Handle(
        DepositMoneyCommand request,
        CancellationToken cancellationToken)
    {
        Ensure.NotNull(request);

        // A deposit mutates the balance, so take an EXCLUSIVE (writer) hold on the account: it
        // excludes concurrent mutations and concurrent balance reads of the same account.
        var handle = await _locker
            .AcquireExclusiveAsync(AccountLock.KeyFor(request.AccountId), AccountLock.Options, cancellationToken)
            .ConfigureAwait(false);
        await using var accountLock = handle.ConfigureAwait(false);

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
