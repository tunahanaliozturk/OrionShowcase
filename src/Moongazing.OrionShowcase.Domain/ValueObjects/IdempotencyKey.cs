namespace Moongazing.OrionShowcase.Domain.ValueObjects;

using Moongazing.OrionGuard.Core;

public readonly record struct IdempotencyKey
{
    public string Value { get; }
    public IdempotencyKey(string value)
    {
        // Ensure.NotNullOrWhiteSpace -> ArgumentException (standard contract).
        Ensure.NotNullOrWhiteSpace(value);
        // Length cap is a structural rule. Ensure.InRange -> ArgumentOutOfRangeException.
        Ensure.InRange(value.Length, 1, 128);
        Value = value;
    }
}
