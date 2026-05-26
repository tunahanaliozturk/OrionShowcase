namespace Moongazing.OrionShowcase.Domain.ValueObjects;

using System.Globalization;
using System.Numerics;

public sealed record Iban
{
    public string Value { get; }
    public string CountryCode => Value[..2];

    public Iban(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("IBAN must not be empty.", nameof(value));
        var normalized = value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        if (normalized.Length < 15 || normalized.Length > 34)
            throw new ArgumentException("IBAN length must be between 15 and 34 characters.", nameof(value));
        if (!ValidateMod97(normalized))
            throw new ArgumentException("IBAN failed mod-97 checksum.", nameof(value));
        Value = normalized;
    }

    private static bool ValidateMod97(string iban)
    {
        var rearranged = iban[4..] + iban[..4];
        var sb = new System.Text.StringBuilder(rearranged.Length * 2);
        foreach (var c in rearranged)
        {
            if (char.IsDigit(c)) sb.Append(c);
            else if (c >= 'A' && c <= 'Z') sb.Append((c - 'A' + 10).ToString(CultureInfo.InvariantCulture));
            else return false;
        }
        return BigInteger.Parse(sb.ToString(), CultureInfo.InvariantCulture) % 97 == 1;
    }
}
