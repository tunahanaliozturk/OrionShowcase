namespace Moongazing.OrionShowcase.Application.Accounts.Commands.FreezeAccount;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Application.Pipeline;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record FreezeAccountCommand(AccountId AccountId, string Reason)
    : IRequest<Result<Unit>>, IAuditableCommand;
