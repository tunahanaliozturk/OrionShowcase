namespace Moongazing.OrionShowcase.Application.Tests.Accounts;

using FluentAssertions;
using Moongazing.OrionShowcase.Application.Accounts.Commands.OpenAccount;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class OpenAccountHandlerTests
{
    private const string ValidIban = "TR330006100519786457841326";

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public Dictionary<AccountId, Account> Store { get; } = new();

        public Task AddAsync(Account account, CancellationToken cancellationToken)
        {
            Store[account.Id] = account;
            return Task.CompletedTask;
        }

        public Task<Account?> GetAsync(AccountId id, CancellationToken cancellationToken)
            => Task.FromResult(Store.GetValueOrDefault(id));
    }

    private sealed class CountingUow : IUnitOfWork
    {
        public int Calls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(1);
        }
    }

    [Fact]
    public async Task Opens_account_with_initial_balance_and_returns_id()
    {
        var repo = new FakeAccountRepo();
        var uow = new CountingUow();
        var sut = new OpenAccountHandler(repo, uow, new FixedClock());

        var cmd = new OpenAccountCommand(
            CustomerId: new CustomerId(Guid.NewGuid()),
            Iban: ValidIban,
            OpeningAmount: 100m,
            Currency: Currency.TRY,
            IdempotencyKey: new IdempotencyKey("open-1"));

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccountId.Should().NotBe(Guid.Empty);
        repo.Store.Should().ContainSingle();
        repo.Store.Values.Single().Balance.Amount.Should().Be(100m);
        repo.Store.Values.Single().Balance.Currency.Should().Be(Currency.TRY);
        uow.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Account_is_active_with_correct_customer_id_after_open()
    {
        var repo = new FakeAccountRepo();
        var uow = new CountingUow();
        var sut = new OpenAccountHandler(repo, uow, new FixedClock());
        var customerId = new CustomerId(Guid.NewGuid());

        var cmd = new OpenAccountCommand(
            CustomerId: customerId,
            Iban: ValidIban,
            OpeningAmount: 0m,
            Currency: Currency.USD,
            IdempotencyKey: new IdempotencyKey("open-2"));

        var result = await sut.Handle(cmd, CancellationToken.None);

        var account = repo.Store.Values.Single();
        account.Status.Should().Be(AccountStatus.Active);
        account.CustomerId.Should().Be(customerId);
        account.Id.Value.Should().Be(result.Value!.AccountId);
    }
}
