namespace Moongazing.OrionShowcase.Domain.ValueObjects;

public readonly record struct IdempotencyKey
{
    public string Value { get; }
    public IdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("IdempotencyKey must not be empty.", nameof(value));
        if (value.Length > 128)
            throw new ArgumentException("IdempotencyKey must be 128 characters or fewer.", nameof(value));
        Value = value;
    }
}
