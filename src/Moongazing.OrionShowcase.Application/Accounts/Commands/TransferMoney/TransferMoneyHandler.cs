namespace Moongazing.OrionShowcase.Application.Accounts.Commands.TransferMoney;

using System.Globalization;
using MediatR;
using Moongazing.OrionLock;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class TransferMoneyHandler
    : IRequestHandler<TransferMoneyCommand, Result<TransferMoneyResult>>
{
    private static readonly DistributedLockOptions LockOptions = new()
    {
        LeaseDuration = TimeSpan.FromSeconds(30),
        WaitTimeout = TimeSpan.FromSeconds(10),
        RetryInterval = TimeSpan.FromMilliseconds(250),
        AutoRenew = true,
    };

    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _uow;
    private readonly IDistributedLock _locker;
    private readonly IClock _clock;

    public TransferMoneyHandler(
        IAccountRepository accounts,
        IUnitOfWork uow,
        IDistributedLock locker,
        IClock clock)
    {
        _accounts = accounts;
        _uow = uow;
        _locker = locker;
        _clock = clock;
    }

    public async Task<Result<TransferMoneyResult>> Handle(
        TransferMoneyCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Sort the two account ids so two concurrent transfers between the same
        // pair always acquire the locks in the same order. This avoids the classic
        // deadlock pattern (A then B vs B then A).
        var firstId = request.From;
        var secondId = request.To;
        if (string.CompareOrdinal(
                firstId.Value.ToString("N", CultureInfo.InvariantCulture),
                secondId.Value.ToString("N", CultureInfo.InvariantCulture)) > 0)
        {
            (firstId, secondId) = (secondId, firstId);
        }

        var firstKey = LockKey(firstId);
        var secondKey = LockKey(secondId);

        var firstHandle = await _locker
            .AcquireAsync(firstKey, LockOptions, cancellationToken)
            .ConfigureAwait(false);
        await using var firstLock = firstHandle.ConfigureAwait(false);

        var secondHandle = await _locker
            .AcquireAsync(secondKey, LockOptions, cancellationToken)
            .ConfigureAwait(false);
        await using var secondLock = secondHandle.ConfigureAwait(false);

        var from = await _accounts.GetAsync(request.From, cancellationToken).ConfigureAwait(false);
        if (from is null)
        {
            return Result<TransferMoneyResult>.Fail($"Source account '{request.From.Value}' was not found.");
        }

        var to = await _accounts.GetAsync(request.To, cancellationToken).ConfigureAwait(false);
        if (to is null)
        {
            return Result<TransferMoneyResult>.Fail($"Destination account '{request.To.Value}' was not found.");
        }

        try
        {
            from.Withdraw(request.Amount, request.IdempotencyKey, _clock);
            to.Deposit(request.Amount, request.IdempotencyKey, _clock);
            from.RecordTransfer(to.Id, request.Amount, request.IdempotencyKey, _clock);
        }
        catch (InsufficientFundsException ex)
        {
            return Result<TransferMoneyResult>.Fail(ex.Message);
        }
        catch (AccountNotActiveException ex)
        {
            return Result<TransferMoneyResult>.Fail(ex.Message);
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Task 10 wires Snowflake ids via OrionKey; for now a Guid is sufficient.
        return Result<TransferMoneyResult>.Ok(
            new TransferMoneyResult(Guid.NewGuid(), from.Balance.Amount));
    }

    private static string LockKey(AccountId id) =>
        $"account:{id.Value.ToString("N", CultureInfo.InvariantCulture)}";
}
