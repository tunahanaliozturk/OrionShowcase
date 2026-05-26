namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountTransactions;

public sealed record TransactionDto(long Id, string Kind, decimal Amount, string Currency, decimal BalanceAfter, DateTimeOffset At);
