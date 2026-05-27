namespace Moongazing.OrionShowcase.Application.Accounts.Commands.CloseAccount;

using Moongazing.OrionGuard.Compatibility;

public sealed class CloseAccountValidator : FluentStyleValidator<CloseAccountCommand>
{
    public CloseAccountValidator()
    {
        RuleFor(x => x.AccountId.Value).NotEqual(Guid.Empty);
    }
}
