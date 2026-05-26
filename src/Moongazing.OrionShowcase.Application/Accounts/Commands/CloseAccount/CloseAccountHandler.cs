namespace Moongazing.OrionShowcase.Application.Accounts.Commands.CloseAccount;

using MediatR;
using Moongazing.OrionShowcase.Application.Common;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;

public sealed class CloseAccountHandler
    : IRequestHandler<CloseAccountCommand, Result<Unit>>
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public CloseAccountHandler(IAccountRepository accounts, IUnitOfWork uow, IClock clock)
    {
        _accounts = accounts;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(
        CloseAccountCommand request,
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
            account.Close(_clock);
        }
        catch (AccountNotEmptyException ex)
        {
            return Result<Unit>.Fail(ex.Message);
        }
        catch (AccountNotActiveException ex)
        {
            return Result<Unit>.Fail(ex.Message);
        }

        await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<Unit>.Ok(Unit.Value);
    }
}
