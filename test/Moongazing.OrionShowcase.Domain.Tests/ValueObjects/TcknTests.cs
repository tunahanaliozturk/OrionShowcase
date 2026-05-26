namespace Moongazing.OrionShowcase.Domain.Tests.ValueObjects;

using FluentAssertions;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class TcknTests
{
    [Theory]
    [InlineData("10000000146")]
    [InlineData("12345678950")]
    public void Constructor_accepts_valid_tckn(string value)
    {
        new Tckn(value).Value.Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    [InlineData("00000000000")]
    [InlineData("12345678901")]
    [InlineData("abcdefghijk")]
    public void Constructor_rejects_invalid_tckn(string value)
    {
        var act = () => new Tckn(value);
        act.Should().Throw<ArgumentException>();
    }
}
