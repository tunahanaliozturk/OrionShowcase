namespace Moongazing.OrionShowcase.Application.Customers.Commands.RegisterCustomer;

using MediatR;
using Moongazing.OrionShowcase.Application.Abstractions;
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
    private readonly INationalIdIndexer _nationalIdIndexer;

    public RegisterCustomerHandler(
        ICustomerRepository customers,
        IUnitOfWork uow,
        IClock clock,
        INationalIdIndexer nationalIdIndexer)
    {
        _customers = customers;
        _uow = uow;
        _clock = clock;
        _nationalIdIndexer = nationalIdIndexer;
    }

    public async Task<Result<RegisterCustomerResult>> Handle(
        RegisterCustomerCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nationalId = new Tckn(request.NationalId);

        // OrionVault deterministic blind index: the national id is persisted as randomized
        // ciphertext, so we also store this HMAC digest alongside it to make exact-match lookups
        // (uniqueness check, "find by national id") possible without decrypting any row.
        var nationalIdIndex = _nationalIdIndexer.Compute(nationalId);

        var customer = Customer.Register(
            request.FullName,
            nationalId,
            nationalIdIndex,
            request.Email,
            request.Phone,
            _clock);

        await _customers.AddAsync(customer, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<RegisterCustomerResult>.Ok(new RegisterCustomerResult(customer.Id.Value));
    }
}
