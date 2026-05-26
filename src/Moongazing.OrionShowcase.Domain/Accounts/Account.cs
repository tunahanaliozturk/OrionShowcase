namespace Moongazing.OrionShowcase.Domain.Accounts;

using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class Account : AggregateRoot<AccountId>
{
    public CustomerId CustomerId { get; private set; }
    public Iban Iban { get; private set; } = null!;
    public Money Balance { get; private set; } = null!;
    public AccountStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    private readonly List<Transaction> _transactions = new();
    public IReadOnlyList<Transaction> Transactions => _transactions;

    private Account() { }   // EF Core ctor

    public static Account Open(CustomerId customer, Iban iban, Money opening, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(iban);
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(clock);

        var id = new AccountId(Guid.NewGuid());
        var account = new Account
        {
            Id = id,
            CustomerId = customer,
            Iban = iban,
            Balance = opening,
            Status = AccountStatus.Active,
            OpenedAt = clock.UtcNow
        };
        account.Raise(new AccountOpened(id, customer, iban, opening, clock.UtcNow));
        return account;
    }

    public void Deposit(Money amount, IdempotencyKey key, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(clock);
        EnsureActive(nameof(Deposit));
        Balance += amount;
        _transactions.Add(new Transaction
        {
            Id = new TransactionId(0),
            Kind = TransactionKind.Deposit,
            Amount = amount,
            BalanceAfter = Balance,
            IdempotencyKey = key,
            At = clock.UtcNow
        });
        Raise(new MoneyDeposited(Id, amount, Balance, key, clock.UtcNow));
    }

    public void Withdraw(Money amount, IdempotencyKey key, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(clock);
        EnsureActive(nameof(Withdraw));
        if (Balance < amount) throw new InsufficientFundsException();
        Balance -= amount;
        _transactions.Add(new Transaction
        {
            Id = new TransactionId(0),
            Kind = TransactionKind.Withdrawal,
            Amount = amount,
            BalanceAfter = Balance,
            IdempotencyKey = key,
            At = clock.UtcNow
        });
        Raise(new MoneyWithdrawn(Id, amount, Balance, key, clock.UtcNow));
    }

    public void RecordTransfer(AccountId counterparty, Money amount, IdempotencyKey key, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(clock);
        Raise(new TransferCompleted(Id, counterparty, amount, key, clock.UtcNow));
    }

    public void Freeze(string reason, IClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(clock);
        EnsureActive(nameof(Freeze));
        Status = AccountStatus.Frozen;
        Raise(new AccountFrozen(Id, reason, clock.UtcNow));
    }

    public void Close(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        EnsureActive(nameof(Close));
        if (Balance.Amount != 0m) throw new AccountNotEmptyException();
        Status = AccountStatus.Closed;
        Raise(new AccountClosed(Id, clock.UtcNow));
    }

    private void EnsureActive(string op)
    {
        if (Status != AccountStatus.Active) throw new AccountNotActiveException(op);
    }
}
