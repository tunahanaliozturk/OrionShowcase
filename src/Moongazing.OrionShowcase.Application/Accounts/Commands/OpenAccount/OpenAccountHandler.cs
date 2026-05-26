namespace Moongazing.OrionShowcase.Application.Accounts.Commands.OpenAccount;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class OpenAccountHandler
    : IRequestHandler<OpenAccountCommand, Result<OpenAccountResult>>
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public OpenAccountHandler(IAccountRepository accounts, IUnitOfWork uow, IClock clock)
    {
        _accounts = accounts;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<OpenAccountResult>> Handle(
        OpenAccountCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var iban = new Iban(request.Iban);
        var opening = new Money(request.OpeningAmount, request.Currency);
        var account = Account.Open(request.CustomerId, iban, opening, _clock);

        await _accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<OpenAccountResult>.Ok(new OpenAccountResult(account.Id.Value));
    }
}
