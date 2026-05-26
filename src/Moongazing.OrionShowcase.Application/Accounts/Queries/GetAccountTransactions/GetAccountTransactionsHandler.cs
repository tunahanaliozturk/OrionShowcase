namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountTransactions;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Repositories;

public sealed class GetAccountTransactionsHandler : IRequestHandler<GetAccountTransactionsQuery, Result<IReadOnlyList<TransactionDto>>>
{
    private readonly IAccountRepository _accounts;

    public GetAccountTransactionsHandler(IAccountRepository accounts) => _accounts = accounts;

    public async Task<Result<IReadOnlyList<TransactionDto>>> Handle(GetAccountTransactionsQuery req, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(req);
        var account = await _accounts.GetAsync(req.AccountId, cancellationToken).ConfigureAwait(false);
        if (account is null) return Result<IReadOnlyList<TransactionDto>>.Fail("Account not found.");

        var dtos = account.Transactions
            .OrderByDescending(t => t.At)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(t => new TransactionDto(
                t.Id.Value, t.Kind.ToString(), t.Amount.Amount, t.Amount.Currency.ToString(),
                t.BalanceAfter.Amount, t.At))
            .ToList();

        return Result<IReadOnlyList<TransactionDto>>.Ok(dtos);
    }
}
