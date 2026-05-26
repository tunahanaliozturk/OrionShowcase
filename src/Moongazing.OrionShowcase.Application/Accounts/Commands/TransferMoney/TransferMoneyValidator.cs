namespace Moongazing.OrionShowcase.Application.Accounts.Commands.TransferMoney;

using FluentValidation;

public sealed class TransferMoneyValidator : AbstractValidator<TransferMoneyCommand>
{
    public TransferMoneyValidator()
    {
        RuleFor(x => x.From.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.To.Value).NotEqual(Guid.Empty);
        RuleFor(x => x).Must(c => c.From.Value != c.To.Value)
            .WithMessage("From and To accounts must differ.");
        RuleFor(x => x.Amount).NotNull();
        RuleFor(x => x.Amount.Amount).GreaterThan(0m).When(x => x.Amount is not null);
        RuleFor(x => x.IdempotencyKey.Value).NotEmpty().MaximumLength(128);
    }
}
