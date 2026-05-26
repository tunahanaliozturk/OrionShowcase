namespace Moongazing.OrionShowcase.Application.Tests.Accounts;

using FluentAssertions;
using MediatR;
using Moongazing.OrionShowcase.Application.Accounts.Commands.FreezeAccount;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class FreezeAccountHandlerTests
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

    private static Account NewActiveAccount(IClock clock)
    {
        return Account.Open(
            new CustomerId(Guid.NewGuid()),
            new Iban(ValidIban),
            new Money(100m, Currency.TRY),
            clock);
    }

    [Fact]
    public async Task Freezes_active_account_and_saves()
    {
        var clock = new FixedClock();
        var repo = new FakeAccountRepo();
        var uow = new CountingUow();
        var account = NewActiveAccount(clock);
        repo.Store[account.Id] = account;
        var sut = new FreezeAccountHandler(repo, uow, clock);

        var cmd = new FreezeAccountCommand(account.Id, "Suspicious activity");

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Unit.Value);
        account.Status.Should().Be(AccountStatus.Frozen);
        uow.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Fails_when_account_not_found()
    {
        var clock = new FixedClock();
        var repo = new FakeAccountRepo();
        var uow = new CountingUow();
        var sut = new FreezeAccountHandler(repo, uow, clock);

        var cmd = new FreezeAccountCommand(new AccountId(Guid.NewGuid()), "Fraud");

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
        uow.Calls.Should().Be(0);
    }
}
