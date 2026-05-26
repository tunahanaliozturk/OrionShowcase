namespace Moongazing.OrionShowcase.Application.Accounts.Commands.FreezeAccount;

using FluentValidation;

public sealed class FreezeAccountValidator : AbstractValidator<FreezeAccountCommand>
{
    public FreezeAccountValidator()
    {
        RuleFor(x => x.AccountId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(512);
    }
}
