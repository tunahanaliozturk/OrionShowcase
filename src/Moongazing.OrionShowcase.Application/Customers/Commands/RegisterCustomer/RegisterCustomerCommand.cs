namespace Moongazing.OrionShowcase.Application.Customers.Commands.RegisterCustomer;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Application.Pipeline;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record RegisterCustomerCommand(
    string FullName,
    string NationalId,
    string Email,
    string Phone,
    IdempotencyKey IdempotencyKey)
    : IRequest<Result<RegisterCustomerResult>>, IAuditableCommand, IIdempotentCommand;

public sealed record RegisterCustomerResult(Guid CustomerId);
