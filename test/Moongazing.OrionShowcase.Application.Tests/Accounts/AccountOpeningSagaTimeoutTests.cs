namespace Moongazing.OrionShowcase.Application.Tests.Accounts;

using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Application.Accounts.Sagas;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

/// <summary>
/// Covers OrionSaga 0.2's per-step timeout and the distinct Cancelled / TimedOut outcomes on the
/// account-opening saga: an overrunning step must roll back and be reported as a timeout (not a
/// business failure), and a caller-token cancellation must be reported as a cancellation that is not
/// a timeout.
/// </summary>
public class AccountOpeningSagaTimeoutTests
{
    private const string ValidIban = "TR330006100519786457841326";

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public Dictionary<AccountId, Account> Store { get; } = new();
        public Task AddAsync(Account account, CancellationToken ct) { Store[account.Id] = account; return Task.CompletedTask; }
        public Task<Account?> GetAsync(AccountId id, CancellationToken ct) => Task.FromResult(Store.GetValueOrDefault(id));
    }

    private sealed class FakeCustomerRepo : ICustomerRepository
    {
        public Dictionary<CustomerId, Customer> Store { get; } = new();
        public Task AddAsync(Customer customer, CancellationToken ct) { Store[customer.Id] = customer; return Task.CompletedTask; }
        public Task<Customer?> GetAsync(CustomerId id, CancellationToken ct) => Task.FromResult(Store.GetValueOrDefault(id));
        public Task<bool> ExistsByNationalIdAsync(Tckn nationalId, CancellationToken ct) => Task.FromResult(false);
        public Task<Customer?> FindByNationalIdAsync(Tckn nationalId, CancellationToken ct) => Task.FromResult<Customer?>(null);
    }

    private sealed class CountingUow : IUnitOfWork
    {
        public int Calls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { Calls++; return Task.FromResult(1); }
    }

    private sealed class InMemoryLimitRegistry : IAccountLimitRegistry
    {
        public Dictionary<AccountId, Money> Limits { get; } = new();
        public void SetDailyLimit(AccountId accountId, Money limit) => Limits[accountId] = limit;
        public void RemoveLimit(AccountId accountId) => Limits.Remove(accountId);
        public Money? GetDailyLimit(AccountId accountId) => Limits.GetValueOrDefault(accountId);
    }

    private static (AccountOpeningSaga Saga, FakeAccountRepo Accounts, CountingUow Uow) Build(CustomerId existing)
    {
        var clock = new FixedClock();
        var accounts = new FakeAccountRepo();
        var customers = new FakeCustomerRepo();
        var uow = new CountingUow();
        var limits = new InMemoryLimitRegistry();

        customers.Store[existing] = Customer.Register(
            "Test Customer", new Tckn("10000000146"), new byte[] { 1, 2, 3 }, "test@example.com", "+905551234567", clock);

        var saga = new AccountOpeningSaga(accounts, customers, uow, limits, clock, NullLogger<AccountOpeningSaga>.Instance);
        return (saga, accounts, uow);
    }

    [Fact]
    public async Task Reports_TimedOut_distinctly_when_first_step_overruns_its_per_step_timeout()
    {
        var customerId = new CustomerId(Guid.NewGuid());
        var (saga, accounts, uow) = Build(customerId);

        // The validate-customer step delays well past its (overridden, small) per-step budget, so the
        // step is cancelled and the saga rolls back.
        var ctx = new AccountOpeningContext(
            customerId,
            ValidIban,
            openingAmount: 100m,
            currency: Currency.TRY,
            idempotencyKey: new IdempotencyKey("open-timeout"),
            validateCustomerDelay: TimeSpan.FromSeconds(30),
            validateCustomerTimeout: TimeSpan.FromMilliseconds(50));

        var (result, _) = await saga.RunAsync(ctx, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Cancelled.Should().BeTrue("a per-step timeout cancels the run");
        result.TimedOut.Should().BeTrue("the cancellation was caused by the step's per-step timeout");
        result.Failed.Should().BeFalse("a timeout is not a business failure");
        result.FailedStep.Should().Be("validate-customer");

        // No later step ran: nothing was persisted and no account was created.
        accounts.Store.Should().BeEmpty();
        uow.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Reports_Cancelled_but_not_TimedOut_when_the_caller_token_is_cancelled()
    {
        var customerId = new CustomerId(Guid.NewGuid());
        var (saga, _, _) = Build(customerId);

        using var cts = new CancellationTokenSource();

        // A delay long enough that the caller's cancellation (below) fires first, with a generous
        // per-step budget so the timeout is NOT the trigger.
        var ctx = new AccountOpeningContext(
            customerId,
            ValidIban,
            openingAmount: 100m,
            currency: Currency.TRY,
            idempotencyKey: new IdempotencyKey("open-cancel"),
            validateCustomerDelay: TimeSpan.FromSeconds(30),
            validateCustomerTimeout: TimeSpan.FromSeconds(30));

        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var (result, _) = await saga.RunAsync(ctx, cts.Token);

        result.Succeeded.Should().BeFalse();
        result.Cancelled.Should().BeTrue("the caller's token was cancelled");
        result.TimedOut.Should().BeFalse("the cancellation came from the caller, not a per-step timeout");
        result.Failed.Should().BeFalse("a cancellation is not a business failure");
    }

    [Fact]
    public async Task Happy_path_still_succeeds_when_no_step_overruns()
    {
        var customerId = new CustomerId(Guid.NewGuid());
        var (saga, accounts, uow) = Build(customerId);

        var ctx = new AccountOpeningContext(
            customerId,
            ValidIban,
            openingAmount: 100m,
            currency: Currency.TRY,
            idempotencyKey: new IdempotencyKey("open-ok"));

        var (result, opened) = await saga.RunAsync(ctx, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Cancelled.Should().BeFalse();
        result.TimedOut.Should().BeFalse();
        opened.AccountId.Should().NotBeNull();
        accounts.Store.Should().ContainSingle();
        uow.Calls.Should().Be(1);
    }
}
