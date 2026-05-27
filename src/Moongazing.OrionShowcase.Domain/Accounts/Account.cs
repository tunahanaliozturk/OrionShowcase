namespace Moongazing.OrionShowcase.Domain.Accounts;

using Moongazing.OrionGuard.Core;
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
        // Ensure.NotNull keeps the ArgumentNullException contract for these reference args.
        Ensure.NotNull(iban);
        Ensure.NotNull(opening);
        Ensure.NotNull(clock);
        // Domain precondition: opening deposit cannot be negative. (Money already enforces
        // this in its constructor, but stating the contract explicitly documents intent.)
        Contract.Requires(opening.Amount >= 0m, "Opening deposit must be non-negative.");

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
        // Demonstration site for FluentGuard: a single chain handles null + the
        // "positive-amount" business rule. NotNull throws first if amount is null;
        // Must runs after and uses the strongly-typed lambda over Money.
        Ensure.For(amount, nameof(amount))
            .NotNull()
            .Must(static m => m.Amount > 0m, "Deposit amount must be positive.")
            .Build();
        Ensure.NotNull(clock);
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
        Ensure.NotNull(amount);
        Ensure.NotNull(clock);
        EnsureActive(nameof(Withdraw));
        // Domain rule: insufficient funds is a business exception (not a precondition
        // violation) and stays as InsufficientFundsException for callers to catch.
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
        Ensure.NotNull(amount);
        Ensure.NotNull(clock);
        // Design-by-Contract: stating the preconditions of RecordTransfer in one place.
        Contract.Requires(amount.Amount > 0m, "Transfer amount must be positive.");
        Contract.Requires(counterparty.Value != Id.Value, "Cannot transfer to the same account.");
        Raise(new TransferCompleted(Id, counterparty, amount, key, clock.UtcNow));
    }

    public void Freeze(string reason, IClock clock)
    {
        Ensure.NotNullOrWhiteSpace(reason);
        Ensure.NotNull(clock);
        EnsureActive(nameof(Freeze));
        Status = AccountStatus.Frozen;
        Raise(new AccountFrozen(Id, reason, clock.UtcNow));
    }

    public void Close(IClock clock)
    {
        Ensure.NotNull(clock);
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
