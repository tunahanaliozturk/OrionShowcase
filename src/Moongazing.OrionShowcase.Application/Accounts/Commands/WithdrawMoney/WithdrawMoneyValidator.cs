namespace Moongazing.OrionShowcase.Application.Accounts.Commands.WithdrawMoney;

using FluentValidation;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class WithdrawMoneyValidator : AbstractValidator<WithdrawMoneyCommand>
{
    public WithdrawMoneyValidator()
    {
        RuleFor(x => x.AccountId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Currency).NotEqual(Currency.None);
        RuleFor(x => x.IdempotencyKey.Value).NotEmpty().MaximumLength(128);
    }
}
