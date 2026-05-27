namespace Moongazing.OrionShowcase.Application.Accounts.Commands.FreezeAccount;

using Moongazing.OrionGuard.Compatibility;

public sealed class FreezeAccountValidator : FluentStyleValidator<FreezeAccountCommand>
{
    public FreezeAccountValidator()
    {
        RuleFor(x => x.AccountId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(512);
    }
}
