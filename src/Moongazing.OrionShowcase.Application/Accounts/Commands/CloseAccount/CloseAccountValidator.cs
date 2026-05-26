namespace Moongazing.OrionShowcase.Application.Accounts.Commands.CloseAccount;

using FluentValidation;

public sealed class CloseAccountValidator : AbstractValidator<CloseAccountCommand>
{
    public CloseAccountValidator()
    {
        RuleFor(x => x.AccountId.Value).NotEqual(Guid.Empty);
    }
}
