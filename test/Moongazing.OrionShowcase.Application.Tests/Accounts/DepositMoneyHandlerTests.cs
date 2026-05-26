namespace Moongazing.OrionShowcase.Application.Tests.Accounts;

using FluentAssertions;
using Moongazing.OrionShowcase.Application.Accounts.Commands.DepositMoney;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class DepositMoneyHandlerTests
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

    private static Account NewActiveAccount(IClock clock, decimal opening = 100m, Currency currency = Currency.TRY)
    {
        return Account.Open(
            new CustomerId(Guid.NewGuid()),
            new Iban(ValidIban),
            new Money(opening, currency),
            clock);
    }

    [Fact]
    public async Task Deposits_amount_and_returns_new_balance()
    {
        var clock = new FixedClock();
        var repo = new FakeAccountRepo();
        var uow = new CountingUow();
        var account = NewActiveAccount(clock);
        repo.Store[account.Id] = account;
        var sut = new DepositMoneyHandler(repo, uow, clock);

        var cmd = new DepositMoneyCommand(
            AccountId: account.Id,
            Amount: 50m,
            Currency: Currency.TRY,
            IdempotencyKey: new IdempotencyKey("dep-1"));

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NewBalance.Should().Be(150m);
        uow.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Fails_when_account_not_found()
    {
        var clock = new FixedClock();
        var repo = new FakeAccountRepo();
        var uow = new CountingUow();
        var sut = new DepositMoneyHandler(repo, uow, clock);

        var cmd = new DepositMoneyCommand(
            AccountId: new AccountId(Guid.NewGuid()),
            Amount: 50m,
            Currency: Currency.TRY,
            IdempotencyKey: new IdempotencyKey("dep-2"));

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
        uow.Calls.Should().Be(0);
    }
}
