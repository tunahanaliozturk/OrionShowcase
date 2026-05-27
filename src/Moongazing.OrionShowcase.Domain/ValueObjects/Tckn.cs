namespace Moongazing.OrionShowcase.Domain.ValueObjects;

using Moongazing.OrionGuard.Core;

public sealed record Tckn
{
    public string Value { get; }

    public Tckn(string value)
    {
        // Ensure shorthand throws standard ArgumentException for empty -- preserves
        // the long-standing .NET contract for argument validation on this type.
        Ensure.NotNullOrWhiteSpace(value);

        // FluentGuard via Ensure.For lets us compose structural string rules in one chain.
        // The trailing Build() executes (Build is also invoked implicitly by FluentGuard's
        // implicit conversion to T, but calling it explicitly makes the intent obvious).
        Ensure.For(value, nameof(value))
            .MinLength(11, "TCKN must be exactly 11 digits.")
            .MaxLength(11, "TCKN must be exactly 11 digits.")
            .Must(static v => v.All(char.IsDigit), "TCKN must contain only digits.")
            .Must(static v => v[0] != '0', "TCKN first digit cannot be zero.")
            .Build();

        var digits = value.Select(c => c - '0').ToArray();
        int oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        int evenSum = digits[1] + digits[3] + digits[5] + digits[7];
        int tenth = (oddSum * 7 - evenSum) % 10;
        if (tenth < 0) tenth += 10;

        // Checksum rules are domain invariants -- Contract.Requires expresses them
        // declaratively and throws ContractException with a clear precondition message.
        Contract.Requires(tenth == digits[9], "TCKN failed checksum (10th digit).");

        int total = 0;
        for (int i = 0; i < 10; i++) total += digits[i];
        Contract.Requires(total % 10 == digits[10], "TCKN failed checksum (11th digit).");

        Value = value;
    }
}
