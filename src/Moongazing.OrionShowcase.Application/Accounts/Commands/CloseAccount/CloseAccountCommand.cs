namespace Moongazing.OrionShowcase.Application.Accounts.Commands.CloseAccount;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Application.Pipeline;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record CloseAccountCommand(AccountId AccountId)
    : IRequest<Result<Unit>>, IAuditableCommand;
