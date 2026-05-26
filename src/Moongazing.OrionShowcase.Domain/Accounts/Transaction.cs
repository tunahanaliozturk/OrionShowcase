namespace Moongazing.OrionShowcase.Domain.Accounts;

using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class Transaction
{
    public TransactionId Id { get; init; }
    public TransactionKind Kind { get; init; }
    public Money Amount { get; init; } = Money.Zero(Currency.TRY);
    public Money BalanceAfter { get; init; } = Money.Zero(Currency.TRY);
    public IdempotencyKey IdempotencyKey { get; init; }
    public DateTimeOffset At { get; init; }
}
