namespace Moongazing.OrionShowcase.Application.Accounts.Commands.OpenAccount;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Application.Pipeline;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record OpenAccountCommand(
    CustomerId CustomerId,
    string Iban,
    decimal OpeningAmount,
    Currency Currency,
    IdempotencyKey IdempotencyKey)
    : IRequest<Result<OpenAccountResult>>, IAuditableCommand, IIdempotentCommand;

public sealed record OpenAccountResult(Guid AccountId);
