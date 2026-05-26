namespace Moongazing.OrionShowcase.Domain.Accounts;

using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed record AccountOpened(AccountId AccountId, CustomerId CustomerId, Iban Iban, Money Opening, DateTimeOffset At);
public sealed record MoneyDeposited(AccountId AccountId, Money Amount, Money NewBalance, IdempotencyKey Key, DateTimeOffset At);
public sealed record MoneyWithdrawn(AccountId AccountId, Money Amount, Money NewBalance, IdempotencyKey Key, DateTimeOffset At);
public sealed record TransferCompleted(AccountId From, AccountId To, Money Amount, IdempotencyKey Key, DateTimeOffset At);
public sealed record AccountFrozen(AccountId AccountId, string Reason, DateTimeOffset At);
public sealed record AccountClosed(AccountId AccountId, DateTimeOffset At);
