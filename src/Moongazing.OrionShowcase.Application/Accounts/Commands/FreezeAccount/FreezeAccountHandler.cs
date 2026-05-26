namespace Moongazing.OrionShowcase.Application.Accounts.Commands.FreezeAccount;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;

public sealed class FreezeAccountHandler
    : IRequestHandler<FreezeAccountCommand, Result<Unit>>
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public FreezeAccountHandler(IAccountRepository accounts, IUnitOfWork uow, IClock clock)
    {
        _accounts = accounts;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(
        FreezeAccountCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var account = await _accounts.GetAsync(request.AccountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return Result<Unit>.Fail($"Account '{request.AccountId.Value}' was not found.");
        }

        try
        {
            account.Freeze(request.Reason, _clock);
        }
        catch (AccountNotActiveException ex)
        {
            return Result<Unit>.Fail(ex.Message);
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<Unit>.Ok(Unit.Value);
    }
}
