namespace Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record Tckn
{
    public string Value { get; }

    public Tckn(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 11 || !value.All(char.IsDigit))
            throw new ArgumentException("TCKN must be exactly 11 digits.", nameof(value));
        if (value[0] == '0')
            throw new ArgumentException("TCKN first digit cannot be zero.", nameof(value));

        var digits = value.Select(c => c - '0').ToArray();
        int oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        int evenSum = digits[1] + digits[3] + digits[5] + digits[7];
        int tenth = (oddSum * 7 - evenSum) % 10;
        if (tenth < 0) tenth += 10;
        if (tenth != digits[9])
            throw new ArgumentException("TCKN failed checksum (10th digit).", nameof(value));
        int total = 0;
        for (int i = 0; i < 10; i++) total += digits[i];
        if (total % 10 != digits[10])
            throw new ArgumentException("TCKN failed checksum (11th digit).", nameof(value));

        Value = value;
    }
}
