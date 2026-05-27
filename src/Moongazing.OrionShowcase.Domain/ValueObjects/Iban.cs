namespace Moongazing.OrionShowcase.Domain.ValueObjects;

using System.Globalization;
using System.Numerics;
using Moongazing.OrionGuard.Core;

public sealed record Iban
{
    public string Value { get; }
    public string CountryCode => Value[..2];

    public Iban(string value)
    {
        // Ensure.NotNullOrWhiteSpace throws standard ArgumentException -- preserves
        // .NET argument-validation contract for the empty-input case.
        Ensure.NotNullOrWhiteSpace(value);

        var normalized = value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

        // Length is a structural rule; Ensure.InRange throws ArgumentOutOfRangeException
        // (which is an ArgumentException), keeping callers' exception-type expectations intact.
        Ensure.InRange(normalized.Length, 15, 34);

        // Checksum is a true domain invariant -- Contract.Requires expresses precondition
        // semantics ("this value must satisfy mod-97") and throws ContractException.
        Contract.Requires(ValidateMod97(normalized), "IBAN failed mod-97 checksum.");

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
