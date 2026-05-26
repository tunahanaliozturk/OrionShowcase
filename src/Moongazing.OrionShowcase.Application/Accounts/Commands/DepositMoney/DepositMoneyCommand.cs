namespace Moongazing.OrionShowcase.Application.Accounts.Commands.DepositMoney;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Application.Pipeline;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record DepositMoneyCommand(
    AccountId AccountId,
    decimal Amount,
    Currency Currency,
    IdempotencyKey IdempotencyKey)
    : IRequest<Result<DepositMoneyResult>>, IAuditableCommand, IIdempotentCommand;

public sealed record DepositMoneyResult(decimal NewBalance);
