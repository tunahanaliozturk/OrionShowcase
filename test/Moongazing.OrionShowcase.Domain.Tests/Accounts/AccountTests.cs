namespace Moongazing.OrionShowcase.Domain.Tests.Accounts;

using FluentAssertions;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class AccountTests
{
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; init; } = DateTimeOffset.UnixEpoch; }

    private static Account Open(decimal amount = 100m, Currency c = Currency.TRY) =>
        Account.Open(
            new CustomerId(Guid.NewGuid()),
            new Iban("TR330006100519786457841326"),
            new Money(amount, c),
            new FixedClock());

    [Fact]
    public void Open_raises_AccountOpened_and_sets_balance()
    {
        var account = Open(100m);
        account.Balance.Should().Be(new Money(100m, Currency.TRY));
        account.Status.Should().Be(AccountStatus.Active);
        account.DomainEvents.Should().ContainSingle(e => e is AccountOpened);
    }

    [Fact]
    public void Deposit_increases_balance_and_records_transaction_and_event()
    {
        var account = Open(100m);
        account.ClearDomainEvents();

        account.Deposit(new Money(50m, Currency.TRY), new IdempotencyKey("k1"), new FixedClock());

        account.Balance.Should().Be(new Money(150m, Currency.TRY));
        account.Transactions.Should().ContainSingle(t => t.Kind == TransactionKind.Deposit);
        account.DomainEvents.Should().ContainSingle(e => e is MoneyDeposited);
    }

    [Fact]
    public void Deposit_with_different_currency_throws()
    {
        var account = Open(100m, Currency.TRY);
        var act = () => account.Deposit(new Money(50m, Currency.USD), new IdempotencyKey("k1"), new FixedClock());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Withdraw_decreases_balance()
    {
        var account = Open(100m);
        account.Withdraw(new Money(40m, Currency.TRY), new IdempotencyKey("k1"), new FixedClock());
        account.Balance.Should().Be(new Money(60m, Currency.TRY));
    }

    [Fact]
    public void Withdraw_more_than_balance_throws_InsufficientFundsException()
    {
        var account = Open(100m);
        var act = () => account.Withdraw(new Money(150m, Currency.TRY), new IdempotencyKey("k1"), new FixedClock());
        act.Should().Throw<InsufficientFundsException>();
    }

    [Fact]
    public void Freeze_changes_status_and_subsequent_deposit_throws()
    {
        var account = Open(100m);
        account.Freeze("manual review", new FixedClock());
        account.Status.Should().Be(AccountStatus.Frozen);
        var act = () => account.Deposit(new Money(10m, Currency.TRY), new IdempotencyKey("k1"), new FixedClock());
        act.Should().Throw<AccountNotActiveException>();
    }

    [Fact]
    public void Close_requires_zero_balance()
    {
        var account = Open(100m);
        var act = () => account.Close(new FixedClock());
        act.Should().Throw<AccountNotEmptyException>();
    }

    [Fact]
    public void Close_with_zero_balance_succeeds()
    {
        var account = Open(0m);
        account.Close(new FixedClock());
        account.Status.Should().Be(AccountStatus.Closed);
        account.DomainEvents.Should().Contain(e => e is AccountClosed);
    }
}
