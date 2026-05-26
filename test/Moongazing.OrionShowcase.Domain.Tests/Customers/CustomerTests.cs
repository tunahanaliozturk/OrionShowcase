namespace Moongazing.OrionShowcase.Domain.Tests.Customers;

using FluentAssertions;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class CustomerTests
{
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; init; } = DateTimeOffset.UnixEpoch; }

    [Fact]
    public void Register_raises_CustomerRegistered_event_and_sets_properties()
    {
        var c = Customer.Register(
            "Ali Veli",
            new Tckn("10000000146"),
            "ali@example.com",
            "+905551234567",
            new FixedClock());

        c.FullName.Should().Be("Ali Veli");
        c.NationalId.Value.Should().Be("10000000146");
        c.Email.Should().Be("ali@example.com");
        c.Phone.Should().Be("+905551234567");
        c.DomainEvents.Should().ContainSingle(e => e is CustomerRegistered);
    }

    [Fact]
    public void Register_with_blank_name_throws()
    {
        var act = () => Customer.Register(" ", new Tckn("10000000146"), "ali@x.com", "+905551234567", new FixedClock());
        act.Should().Throw<ArgumentException>();
    }
}
