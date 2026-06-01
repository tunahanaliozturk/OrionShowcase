namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountTransactions;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Repositories;

public sealed class GetAccountTransactionsHandler : IRequestHandler<GetAccountTransactionsQuery, Result<IReadOnlyList<TransactionDto>>>
{
    private readonly IAccountRepository _accounts;

    public GetAccountTransactionsHandler(IAccountRepository accounts) => _accounts = accounts;

    public async Task<Result<IReadOnlyList<TransactionDto>>> Handle(GetAccountTransactionsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await _accounts.GetAsync(request.AccountId, cancellationToken).ConfigureAwait(false);
        if (account is null) return Result<IReadOnlyList<TransactionDto>>.Fail("Account not found.");

        var dtos = account.Transactions
            .OrderByDescending(t => t.At)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TransactionDto(
                t.Id.Value, t.Kind.ToString(), t.Amount.Amount, t.Amount.Currency.ToString(),
                t.BalanceAfter.Amount, t.At))
            .ToList();

        return Result<IReadOnlyList<TransactionDto>>.Ok(dtos);
    }
}
