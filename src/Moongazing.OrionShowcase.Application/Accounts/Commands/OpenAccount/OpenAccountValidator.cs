namespace Moongazing.OrionShowcase.Application.Accounts.Commands.OpenAccount;

using FluentValidation;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class OpenAccountValidator : AbstractValidator<OpenAccountCommand>
{
    public OpenAccountValidator()
    {
        RuleFor(x => x.CustomerId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.Iban).NotEmpty().MaximumLength(34);
        RuleFor(x => x.OpeningAmount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Currency).NotEqual(Currency.None);
        RuleFor(x => x.IdempotencyKey.Value).NotEmpty().MaximumLength(128);
    }
}
