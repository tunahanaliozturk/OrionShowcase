namespace Moongazing.OrionShowcase.Application.Customers.Commands.RegisterCustomer;

using Moongazing.OrionGuard.Compatibility;

public sealed class RegisterCustomerValidator : FluentStyleValidator<RegisterCustomerCommand>
{
    public RegisterCustomerValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NationalId).NotEmpty().Length(11, 11).Matches("^[0-9]{11}$");
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(32);
        RuleFor(x => x.IdempotencyKey.Value).NotEmpty().MaximumLength(128);
    }
}
