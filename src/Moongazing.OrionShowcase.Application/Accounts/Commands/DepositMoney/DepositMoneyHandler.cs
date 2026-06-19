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
    private readonly IDistributedLock _locker;
    private readonly ISharedExclusiveLock _readerWriter;
    private readonly IClock _clock;

    public DepositMoneyHandler(
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

    public async Task<Result<DepositMoneyResult>> Handle(
        DepositMoneyCommand request,
        CancellationToken cancellationToken)
    {
        Ensure.NotNull(request);

        var key = AccountLock.KeyFor(request.AccountId);

        // A deposit mutates the balance, so take the DISTRIBUTED (Postgres advisory) lock on the
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
