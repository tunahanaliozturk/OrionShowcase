namespace Moongazing.OrionShowcase.Application.Tests.Accounts;

using FluentAssertions;
using Moongazing.OrionLock;
using Moongazing.OrionShowcase.Application.Accounts.Commands.WithdrawMoney;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class WithdrawMoneyHandlerTests
{
    private const string ValidIban = "TR330006100519786457841326";

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public Dictionary<AccountId, Account> Store { get; } = new();
        public Task AddAsync(Account a, CancellationToken ct) { Store[a.Id] = a; return Task.CompletedTask; }
        public Task<Account?> GetAsync(AccountId id, CancellationToken ct) => Task.FromResult(Store.GetValueOrDefault(id));
    }

    private sealed class CountingUow : IUnitOfWork
    {
        public int Calls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { Calls++; return Task.FromResult(1); }
    }

    private static Account NewActiveAccount(IClock clock, decimal opening = 100m)
    {
        return Account.Open(
            new CustomerId(Guid.NewGuid()),
            new Iban(ValidIban),
            new Money(opening, Currency.TRY),
            clock);
    }

    [Fact]
    public async Task Withdraws_amount_and_returns_new_balance()
    {
        var clock = new FixedClock();
        var repo = new FakeAccountRepo();
        var uow = new CountingUow();
        var account = NewActiveAccount(clock, opening: 100m);
        repo.Store[account.Id] = account;
        var locker = new StubSharedExclusiveLock();
        var sut = new WithdrawMoneyHandler(repo, uow, locker, clock);

        var cmd = new WithdrawMoneyCommand(
            AccountId: account.Id,
            Amount: 30m,
            Currency: Currency.TRY,
            IdempotencyKey: new IdempotencyKey("wd-1"));

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NewBalance.Should().Be(70m);
        uow.Calls.Should().Be(1);
        // A withdrawal mutates the balance, so it takes an exclusive hold and releases it.
        locker.AcquiredModes.Should().ContainSingle().Which.Should().Be(LockMode.Exclusive);
        locker.Released.Should().Be(1);
    }

    [Fact]
    public async Task Fails_when_insufficient_funds()
    {
        var clock = new FixedClock();
        var repo = new FakeAccountRepo();
        var uow = new CountingUow();
        var account = NewActiveAccount(clock, opening: 10m);
        repo.Store[account.Id] = account;
        var sut = new WithdrawMoneyHandler(repo, uow, new StubSharedExclusiveLock(), clock);

        var cmd = new WithdrawMoneyCommand(
            AccountId: account.Id,
            Amount: 50m,
            Currency: Currency.TRY,
            IdempotencyKey: new IdempotencyKey("wd-2"));

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Insufficient funds");
        uow.Calls.Should().Be(0);
        account.Balance.Amount.Should().Be(10m);
    }
}
