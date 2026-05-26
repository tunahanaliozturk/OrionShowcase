namespace Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Money amount must be non-negative.");
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
        if (result < 0m)
            throw new InvalidOperationException("Money subtraction would produce a negative amount.");
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
        ArgumentNullException.ThrowIfNull(a);
        return a.CompareTo(b);
    }

    private static void EnsureSameCurrency(Money a, Money b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Currency != b.Currency)
            throw new InvalidOperationException(
                $"Cannot operate on Money values with different currency: {a.Currency} vs {b.Currency}.");
    }
}
