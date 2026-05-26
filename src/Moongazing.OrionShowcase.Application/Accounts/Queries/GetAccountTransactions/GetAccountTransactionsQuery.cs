namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountTransactions;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record GetAccountTransactionsQuery(AccountId AccountId, int Page = 1, int PageSize = 50)
    : IRequest<Result<IReadOnlyList<TransactionDto>>>;
