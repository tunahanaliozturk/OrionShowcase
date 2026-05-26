namespace Moongazing.OrionShowcase.Application.Customers.Commands.RegisterCustomer;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class RegisterCustomerHandler
    : IRequestHandler<RegisterCustomerCommand, Result<RegisterCustomerResult>>
{
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public RegisterCustomerHandler(ICustomerRepository customers, IUnitOfWork uow, IClock clock)
    {
        _customers = customers;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<RegisterCustomerResult>> Handle(
        RegisterCustomerCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nationalId = new Tckn(request.NationalId);
        var customer = Customer.Register(
            request.FullName,
            nationalId,
            request.Email,
            request.Phone,
            _clock);

        await _customers.AddAsync(customer, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<RegisterCustomerResult>.Ok(new RegisterCustomerResult(customer.Id.Value));
    }
}
