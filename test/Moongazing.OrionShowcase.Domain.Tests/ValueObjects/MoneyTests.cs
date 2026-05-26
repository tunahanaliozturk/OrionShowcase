namespace Moongazing.OrionShowcase.Domain.Tests.ValueObjects;

using FluentAssertions;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class MoneyTests
{
    [Fact]
    public void Constructor_throws_when_amount_negative()
    {
        var act = () => new Money(-1m, Currency.TRY);
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
        act.Should().Throw<InvalidOperationException>().WithMessage("*currency*");
    }

    [Fact]
    public void Subtract_resulting_in_negative_throws()
    {
        var a = new Money(50m, Currency.TRY);
        var b = new Money(100m, Currency.TRY);
        var act = () => { var _ = a - b; };
        act.Should().Throw<InvalidOperationException>().WithMessage("*negative*");
    }
}
