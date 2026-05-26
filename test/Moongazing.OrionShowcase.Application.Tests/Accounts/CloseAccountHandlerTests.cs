namespace Moongazing.OrionShowcase.Application.Tests.Accounts;

using FluentAssertions;
using MediatR;
using Moongazing.OrionShowcase.Application.Accounts.Commands.CloseAccount;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class CloseAccountHandlerTests
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

    private static Account NewActiveAccount(IClock clock, decimal opening)
    {
        return Account.Open(
            new CustomerId(Guid.NewGuid()),
            new Iban(ValidIban),
            new Money(opening, Currency.TRY),
            clock);
    }

    [Fact]
    public async Task Closes_account_with_zero_balance()
    {
        var clock = new FixedClock();
        var repo = new FakeAccountRepo();
        var uow = new CountingUow();
        var account = NewActiveAccount(clock, opening: 0m);
        repo.Store[account.Id] = account;
        var sut = new CloseAccountHandler(repo, uow, clock);

        var cmd = new CloseAccountCommand(account.Id);

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Unit.Value);
        account.Status.Should().Be(AccountStatus.Closed);
        uow.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Fails_when_balance_is_non_zero()
    {
        var clock = new FixedClock();
        var repo = new FakeAccountRepo();
        var uow = new CountingUow();
        var account = NewActiveAccount(clock, opening: 25m);
        repo.Store[account.Id] = account;
        var sut = new CloseAccountHandler(repo, uow, clock);

        var cmd = new CloseAccountCommand(account.Id);

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("non-zero balance");
        account.Status.Should().Be(AccountStatus.Active);
        uow.Calls.Should().Be(0);
    }
}
