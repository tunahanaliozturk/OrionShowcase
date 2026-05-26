namespace Moongazing.OrionShowcase.Application.Accounts.Commands.TransferMoney;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Application.Pipeline;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record TransferMoneyCommand(
    AccountId From,
    AccountId To,
    Money Amount,
    IdempotencyKey IdempotencyKey)
    : IRequest<Result<TransferMoneyResult>>, IAuditableCommand, IIdempotentCommand;

public sealed record TransferMoneyResult(Guid TransferId, decimal NewSourceBalance);
