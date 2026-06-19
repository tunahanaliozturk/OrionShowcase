namespace Moongazing.OrionShowcase.Application.Accounts.Commands.WithdrawMoney;

using MediatR;
using Moongazing.OrionLock;
using Moongazing.OrionShowcase.Application.Accounts;
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
    private readonly IDistributedLock _locker;
    private readonly ISharedExclusiveLock _readerWriter;
    private readonly IClock _clock;

    public WithdrawMoneyHandler(
        IAccountRepository accounts,
        IUnitOfWork uow,
        IDistributedLock locker,
        ISharedExclusiveLock readerWriter,
        IClock clock)
    {
        _accounts = accounts;
        _uow = uow;
        _locker = locker;
        _readerWriter = readerWriter;
        _clock = clock;
    }

    public async Task<Result<WithdrawMoneyResult>> Handle(
        WithdrawMoneyCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = AccountLock.KeyFor(request.AccountId);

        // A withdrawal mutates the balance, so take the DISTRIBUTED (Postgres advisory) lock on the
        // account FIRST. This is the real cross-replica safety mechanism: it serializes mutations of
        // the same account across every process/replica, not just within this one.
        var handle = await _locker
            .AcquireAsync(key, AccountLock.Options, cancellationToken)
            .ConfigureAwait(false);
        await using var accountLock = handle.ConfigureAwait(false);

        // Additional IN-PROCESS guard: take an EXCLUSIVE reader-writer hold so a concurrent balance
        // read (GetBalance, SHARED) on this process never observes a half-applied mutation. This is
        // single-process/sample-only and is NOT the cross-replica guarantee; that is the distributed
        // lock above. A distributed reader-writer provider would unify the two in production.
        var rwHandle = await _readerWriter
            .AcquireExclusiveAsync(key, AccountLock.Options, cancellationToken)
            .ConfigureAwait(false);
        await using var readerWriterLock = rwHandle.ConfigureAwait(false);

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
