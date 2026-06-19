namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountBalance;

using MediatR;
using Moongazing.OrionLock;
using Moongazing.OrionShowcase.Application.Accounts;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Repositories;

public sealed class GetAccountBalanceHandler : IRequestHandler<GetAccountBalanceQuery, Result<AccountBalanceDto>>
{
    private readonly IAccountRepository _accounts;
    private readonly ISharedExclusiveLock _locker;

    public GetAccountBalanceHandler(IAccountRepository accounts, ISharedExclusiveLock locker)
    {
        _accounts = accounts;
        _locker = locker;
    }

    public async Task<Result<AccountBalanceDto>> Handle(GetAccountBalanceQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A balance read takes a SHARED (reader) hold on the account: any number of concurrent reads
        // coexist, but the read is excluded while a mutation holds the account exclusively, so a
        // reader never observes a half-applied deposit/withdraw/transfer.
        var handle = await _locker
            .AcquireSharedAsync(AccountLock.KeyFor(request.AccountId), AccountLock.Options, cancellationToken)
            .ConfigureAwait(false);
        await using var accountLock = handle.ConfigureAwait(false);

        var account = await _accounts.GetAsync(request.AccountId, cancellationToken).ConfigureAwait(false);
        if (account is null) return Result<AccountBalanceDto>.Fail("Account not found.");
        return Result<AccountBalanceDto>.Ok(new AccountBalanceDto(
            account.Id.Value, account.Balance.Amount, account.Balance.Currency.ToString(), account.Status.ToString()));
    }
}
