namespace Moongazing.OrionShowcase.Domain.Customers;

using Moongazing.OrionGuard.Core;
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
        // OrionGuard's Ensure shorthand throws standard ArgumentException / ArgumentNullException
        // so existing callers and tests keep working without behavioural change.
        Ensure.NotNullOrWhiteSpace(fullName);
        Ensure.NotNull(nationalId);
        Ensure.NotNullOrWhiteSpace(email);
        Ensure.NotNullOrWhiteSpace(phone);
        Ensure.NotNull(clock);

        // Compose a FluentGuard chain for richer email validation: the Email() rule
        // uses the cached compiled regex shared across the process (RegexCache),
        // so this stays cheap on the hot Register path.
        Ensure.For(email, nameof(email))
            .Email("Email must be a valid email address.")
            .MaxLength(256, "Email is too long.")
            .Build();

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
