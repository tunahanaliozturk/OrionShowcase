namespace Moongazing.OrionShowcase.Domain.ValueObjects;

using Moongazing.OrionGuard.Core;

public sealed record Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        // Use Ensure.InRange so we preserve the standard ArgumentOutOfRangeException
        // contract that callers (and tests) rely on for negative amounts.
        Ensure.InRange(amount, 0m, decimal.MaxValue);
        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        Currency = currency;
    }

    public static Money Zero(Currency currency) => new(0m, currency);

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        var result = a.Amount - b.Amount;
        // Domain invariant: Money cannot be negative. Contract.Requires keeps the
        // arithmetic operator declarative and surfaces a clear semantic violation.
        Contract.Requires(result >= 0m, "Money subtraction would produce a negative amount.");
        return new Money(result, a.Currency);
    }

    public static bool operator <(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a.Amount < b.Amount;
    }

    public static bool operator >(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a.Amount > b.Amount;
    }

    public static bool operator <=(Money a, Money b) => !(a > b);
    public static bool operator >=(Money a, Money b) => !(a < b);

    public Money Add(Money other) => this + other;
    public Money Subtract(Money other) => this - other;

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public static int Compare(Money a, Money b)
    {
        // FastGuard.NotNull uses [MethodImpl(AggressiveInlining)] -- well suited for
        // hot paths like operators and comparators.
        FastGuard.NotNull(a, nameof(a));
        return a.CompareTo(b);
    }

    private static void EnsureSameCurrency(Money a, Money b)
    {
        // Hot path: both operands flow through here from every Money operator.
        FastGuard.NotNull(a, nameof(a));
        FastGuard.NotNull(b, nameof(b));
        // Invariant: cross-currency arithmetic is meaningless and must fail loudly.
        Contract.Invariant(
            a.Currency == b.Currency,
            $"Cannot operate on Money values with different currency: {a.Currency} vs {b.Currency}.");
    }
}
