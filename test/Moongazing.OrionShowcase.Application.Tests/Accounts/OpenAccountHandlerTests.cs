namespace Moongazing.OrionShowcase.Application.Tests.Accounts;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Application.Accounts.Commands.OpenAccount;
using Moongazing.OrionShowcase.Application.Accounts.Sagas;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Customers;
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

    private sealed class FakeCustomerRepo : ICustomerRepository
    {
        public Dictionary<CustomerId, Customer> Store { get; } = new();

        public Task AddAsync(Customer customer, CancellationToken cancellationToken)
        {
            Store[customer.Id] = customer;
            return Task.CompletedTask;
        }

        public Task<Customer?> GetAsync(CustomerId id, CancellationToken cancellationToken)
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

    private sealed class InMemoryLimitRegistry : IAccountLimitRegistry
    {
        public Dictionary<AccountId, Money> Limits { get; } = new();
        public void SetDailyLimit(AccountId accountId, Money limit) => Limits[accountId] = limit;
        public void RemoveLimit(AccountId accountId) => Limits.Remove(accountId);
        public Money? GetDailyLimit(AccountId accountId) => Limits.GetValueOrDefault(accountId);
    }

    private static (OpenAccountHandler Handler, FakeAccountRepo Accounts, CountingUow Uow, InMemoryLimitRegistry Limits)
        BuildHandler(CustomerId existingCustomer)
    {
        var clock = new FixedClock();
        var accounts = new FakeAccountRepo();
        var customers = new FakeCustomerRepo();
        var uow = new CountingUow();
        var limits = new InMemoryLimitRegistry();

        // Seed the customer so the saga's validate-customer step passes.
        customers.Store[existingCustomer] = Customer.Register(
            "Test Customer", new Tckn("10000000146"), "test@example.com", "+905551234567", clock);

        var saga = new AccountOpeningSaga(
            accounts, customers, uow, limits, clock, NullLogger<AccountOpeningSaga>.Instance);
        return (new OpenAccountHandler(saga), accounts, uow, limits);
    }

    [Fact]
    public async Task Opens_account_with_initial_balance_and_returns_id()
    {
        var customerId = new CustomerId(Guid.NewGuid());
        var (sut, repo, uow, limits) = BuildHandler(customerId);

        var cmd = new OpenAccountCommand(
            CustomerId: customerId,
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
        // The saga's set-initial-limit step assigned the default daily limit.
        limits.Limits.Should().ContainSingle();
    }

    [Fact]
    public async Task Account_is_active_with_correct_customer_id_after_open()
    {
        var customerId = new CustomerId(Guid.NewGuid());
        var (sut, repo, _, _) = BuildHandler(customerId);

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
