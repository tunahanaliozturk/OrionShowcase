namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountBalance;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record GetAccountBalanceQuery(AccountId AccountId) : IRequest<Result<AccountBalanceDto>>;
