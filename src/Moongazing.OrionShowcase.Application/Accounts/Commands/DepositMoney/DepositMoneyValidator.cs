namespace Moongazing.OrionShowcase.Application.Accounts.Commands.DepositMoney;

using Moongazing.OrionGuard.Compatibility;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class DepositMoneyValidator : FluentStyleValidator<DepositMoneyCommand>
{
    public DepositMoneyValidator()
    {
        RuleFor(x => x.AccountId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Currency).NotEqual(Currency.None);
        RuleFor(x => x.IdempotencyKey.Value).NotEmpty().MaximumLength(128);
    }
}
