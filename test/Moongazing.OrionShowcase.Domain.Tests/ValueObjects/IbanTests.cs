namespace Moongazing.OrionShowcase.Domain.Tests.ValueObjects;

using FluentAssertions;
using Moongazing.OrionGuard.Core;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class IbanTests
{
    [Theory]
    [InlineData("TR330006100519786457841326")]
    [InlineData("DE89370400440532013000")]
    [InlineData("GB29NWBK60161331926819")]
    public void Constructor_accepts_valid_iban(string value)
    {
        var iban = new Iban(value);
        iban.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("XX")]
    [InlineData("TR3300061005197864578413261234567890123456")]
    public void Constructor_rejects_structurally_invalid_iban(string value)
    {
        var act = () => new Iban(value);
        // Empty/whitespace -> ArgumentException (from Ensure.NotNullOrWhiteSpace).
        // Length out-of-range -> ArgumentOutOfRangeException (which is an ArgumentException).
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_rejects_iban_with_bad_checksum()
    {
        var act = () => new Iban("TR330006100519786457841327");
        // Mod-97 checksum failure is now expressed via Contract.Requires, a domain
        // precondition surfacing as ContractException.
        act.Should().Throw<ContractException>().WithMessage("*mod-97*");
    }

    [Fact]
    public void CountryCode_returns_first_two_letters()
    {
        new Iban("TR330006100519786457841326").CountryCode.Should().Be("TR");
    }
}
