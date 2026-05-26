namespace Moongazing.OrionShowcase.Domain.ValueObjects;

public enum Currency { None = 0, TRY = 949, USD = 840, EUR = 978 }
public enum AccountStatus { None = 0, Active = 1, Frozen = 2, Closed = 3 }
public enum TransactionKind { None = 0, Deposit = 1, Withdrawal = 2, TransferOut = 3, TransferIn = 4 }
