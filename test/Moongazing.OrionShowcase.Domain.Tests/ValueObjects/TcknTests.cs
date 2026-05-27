namespace Moongazing.OrionShowcase.Domain.Tests.ValueObjects;

using FluentAssertions;
using Moongazing.OrionGuard.Core;
using Moongazing.OrionGuard.Exceptions;
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

    [Fact]
    public void Constructor_rejects_empty_tckn()
    {
        var act = () => new Tckn("");
        // Ensure.NotNullOrWhiteSpace -> ArgumentException.
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("1234567890")]    // too short
    [InlineData("123456789012")]  // too long
    [InlineData("abcdefghijk")]   // non-digit
    [InlineData("00000000000")]   // leading zero
    public void Constructor_rejects_structurally_invalid_tckn(string value)
    {
        var act = () => new Tckn(value);
        // FluentGuard (via Ensure.For) throws GuardException for any failing chain rule.
        act.Should().Throw<GuardException>();
    }

    [Fact]
    public void Constructor_rejects_tckn_with_bad_checksum()
    {
        var act = () => new Tckn("12345678901");
        // Checksum rules are now expressed via Contract.Requires -> ContractException.
        act.Should().Throw<ContractException>().WithMessage("*checksum*");
    }
}
