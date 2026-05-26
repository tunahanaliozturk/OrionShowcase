namespace Moongazing.OrionShowcase.Domain.Accounts;

public sealed class InsufficientFundsException : InvalidOperationException
{
    public InsufficientFundsException() : base("Insufficient funds.") { }
    public InsufficientFundsException(string message) : base(message) { }
    public InsufficientFundsException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class AccountNotActiveException : InvalidOperationException
{
    public AccountNotActiveException() : base("Account is not active.") { }
    public AccountNotActiveException(string operation)
        : base($"Account is not active; cannot perform '{operation}'.") { }
    public AccountNotActiveException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class AccountNotEmptyException : InvalidOperationException
{
    public AccountNotEmptyException() : base("Cannot close an account that still has a non-zero balance.") { }
    public AccountNotEmptyException(string message) : base(message) { }
    public AccountNotEmptyException(string message, Exception innerException) : base(message, innerException) { }
}
