namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountBalance;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Repositories;

public sealed class GetAccountBalanceHandler : IRequestHandler<GetAccountBalanceQuery, Result<AccountBalanceDto>>
{
    private readonly IAccountRepository _accounts;

    public GetAccountBalanceHandler(IAccountRepository accounts) => _accounts = accounts;

    public async Task<Result<AccountBalanceDto>> Handle(GetAccountBalanceQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await _accounts.GetAsync(request.AccountId, cancellationToken).ConfigureAwait(false);
        if (account is null) return Result<AccountBalanceDto>.Fail("Account not found.");
        return Result<AccountBalanceDto>.Ok(new AccountBalanceDto(
            account.Id.Value, account.Balance.Amount, account.Balance.Currency.ToString(), account.Status.ToString()));
    }
}
