namespace Moongazing.OrionShowcase.Application.Accounts.Queries.GetAccountBalance;

public sealed record AccountBalanceDto(Guid AccountId, decimal Balance, string Currency, string Status);
