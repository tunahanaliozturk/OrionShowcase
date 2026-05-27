namespace Moongazing.OrionShowcase.Domain.Tests.ValueObjects;

using FluentAssertions;
using Moongazing.OrionGuard.Core;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class MoneyTests
{
    [Fact]
    public void Constructor_throws_when_amount_negative()
    {
        var act = () => new Money(-1m, Currency.TRY);
        // Ensure.InRange preserves the standard ArgumentOutOfRangeException contract.
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Add_same_currency_returns_sum()
    {
        var a = new Money(100m, Currency.TRY);
        var b = new Money(50m, Currency.TRY);
        (a + b).Should().Be(new Money(150m, Currency.TRY));
    }

    [Fact]
    public void Add_different_currency_throws()
    {
        var a = new Money(100m, Currency.TRY);
        var b = new Money(50m, Currency.USD);
        var act = () => { var _ = a + b; };
        // OrionGuard Contract.Invariant surfaces cross-currency arithmetic violations
        // as ContractException ("Invariant violated: ...").
        act.Should().Throw<ContractException>().WithMessage("*currency*");
    }

    [Fact]
    public void Subtract_resulting_in_negative_throws()
    {
        var a = new Money(50m, Currency.TRY);
        var b = new Money(100m, Currency.TRY);
        var act = () => { var _ = a - b; };
        // Contract.Requires surfaces the negative-amount rule as a precondition failure.
        act.Should().Throw<ContractException>().WithMessage("*negative*");
    }
}
