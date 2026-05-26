namespace Moongazing.OrionShowcase.Domain.Customers;

using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record CustomerRegistered(CustomerId CustomerId, DateTimeOffset At);
