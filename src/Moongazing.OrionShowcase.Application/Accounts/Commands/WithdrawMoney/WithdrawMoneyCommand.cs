namespace Moongazing.OrionShowcase.Application.Accounts.Commands.WithdrawMoney;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Application.Pipeline;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record WithdrawMoneyCommand(
    AccountId AccountId,
    decimal Amount,
    Currency Currency,
    IdempotencyKey IdempotencyKey)
    : IRequest<Result<WithdrawMoneyResult>>, IAuditableCommand, IIdempotentCommand;

public sealed record WithdrawMoneyResult(decimal NewBalance);
