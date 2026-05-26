namespace Moongazing.OrionShowcase.Domain.Customers;

using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class Customer : AggregateRoot<CustomerId>
{
    public string FullName { get; private set; } = null!;
    public Tckn NationalId { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public DateTimeOffset RegisteredAt { get; private set; }

    private Customer() { }   // EF Core ctor

    public static Customer Register(string fullName, Tckn nationalId, string email, string phone, IClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentNullException.ThrowIfNull(nationalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        ArgumentNullException.ThrowIfNull(clock);

        var id = new CustomerId(Guid.NewGuid());
        var c = new Customer
        {
            Id = id,
            FullName = fullName.Trim(),
            NationalId = nationalId,
            Email = email.Trim(),
            Phone = phone.Trim(),
            RegisteredAt = clock.UtcNow
        };
        c.Raise(new CustomerRegistered(id, clock.UtcNow));
        return c;
    }
}
