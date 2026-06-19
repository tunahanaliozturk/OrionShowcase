namespace Moongazing.OrionShowcase.Application.Tests.Accounts;

using System.Threading;
using FluentAssertions;
using Moongazing.OrionLock;
using Moongazing.OrionShowcase.Application.Accounts.Commands.TransferMoney;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class TransferMoneyHandlerTests
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

    // Records, in invocation order, the (key, mode) pairs acquired and the keys released over the
    // in-process reader-writer lock, so a test can assert a transfer takes an EXCLUSIVE hold on each
    // account in deterministic (sorted) order and releases both on the success and error paths.
    private sealed class RecordingReaderWriterLock : ISharedExclusiveLock
    {
        public List<(string Key, LockMode Mode)> Acquired { get; } = new();
        public List<string> Released { get; } = new();

        public Task<IDistributedLockHandle> AcquireExclusiveAsync(
            string key, DistributedLockOptions? options = null, CancellationToken cancellationToken = default) =>
            Acquire(key, LockMode.Exclusive);

        public Task<IDistributedLockHandle> AcquireSharedAsync(
            string key, DistributedLockOptions? options = null, CancellationToken cancellationToken = default) =>
            Acquire(key, LockMode.Shared);

        public Task<IDistributedLockHandle?> TryAcquireExclusiveAsync(
            string key, DistributedLockOptions? options = null, CancellationToken cancellationToken = default) =>
            TryAcquire(key, LockMode.Exclusive);

        public Task<IDistributedLockHandle?> TryAcquireSharedAsync(
            string key, DistributedLockOptions? options = null, CancellationToken cancellationToken = default) =>
            TryAcquire(key, LockMode.Shared);

        private Task<IDistributedLockHandle> Acquire(string key, LockMode mode)
        {
            Acquired.Add((key, mode));
            return Task.FromResult<IDistributedLockHandle>(new Handle(key, this));
        }

        private Task<IDistributedLockHandle?> TryAcquire(string key, LockMode mode)
        {
            Acquired.Add((key, mode));
            return Task.FromResult<IDistributedLockHandle?>(new Handle(key, this));
        }

        private sealed class Handle : IDistributedLockHandle
        {
            private readonly RecordingReaderWriterLock _owner;
            public Handle(string key, RecordingReaderWriterLock owner) { Key = key; _owner = owner; }
            public string Key { get; }
            public bool IsHeld { get; private set; } = true;
            public CancellationToken LostToken => CancellationToken.None;
            public ValueTask DisposeAsync()
            {
                if (IsHeld)
                {
                    IsHeld = false;
                    _owner.Released.Add(Key);
                }
                return ValueTask.CompletedTask;
            }
        }
    }

    private static Account NewActiveAccount(IClock clock, decimal opening, AccountId? id = null)
    {
        var account = Account.Open(
            new CustomerId(Guid.NewGuid()),
            new Iban(ValidIban),
            new Money(opening, Currency.TRY),
            clock);

        if (id is { } pinned)
        {
            // Account.Id is `protected init`; reflection is required to pin an id for the test scenario.
            typeof(Account).GetProperty(nameof(Account.Id))!.SetValue(account, pinned);
        }

        return account;
    }

    [Fact]
    public async Task Acquires_locks_on_both_accounts_in_sorted_order()
    {
        var clock = new FixedClock();
        var repo = new FakeAccountRepo();
        var uow = new CountingUow();
        var distributed = new StubDistributedLock();
        var readerWriter = new RecordingReaderWriterLock();

        // Pick two ids whose Guid string ordering is deterministic.
        var lower = new AccountId(new Guid("11111111-1111-1111-1111-111111111111"));
        var higher = new AccountId(new Guid("22222222-2222-2222-2222-222222222222"));

        var from = NewActiveAccount(clock, opening: 100m, id: higher);
        var to = NewActiveAccount(clock, opening: 0m, id: lower);
        repo.Store[from.Id] = from;
        repo.Store[to.Id] = to;

        var sut = new TransferMoneyHandler(repo, uow, distributed, readerWriter, clock);

        var cmd = new TransferMoneyCommand(
            From: from.Id,
            To: to.Id,
            Amount: new Money(40m, Currency.TRY),
            IdempotencyKey: new IdempotencyKey("tx-1"));

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NewSourceBalance.Should().Be(60m);
        from.Balance.Amount.Should().Be(60m);
        to.Balance.Amount.Should().Be(40m);
        uow.Calls.Should().Be(1);

        // The DISTRIBUTED lock (cross-replica safety) is taken on both accounts in sorted id order.
        distributed.Acquired.Should().HaveCount(2);
        distributed.Acquired[0].Should().Contain(lower.Value.ToString("N"));
        distributed.Acquired[1].Should().Contain(higher.Value.ToString("N"));
        distributed.Released.Should().HaveCount(2);

        // The in-process reader-writer holds are EXCLUSIVE and taken in the same sorted order.
        readerWriter.Acquired.Should().HaveCount(2);
        readerWriter.Acquired[0].Mode.Should().Be(LockMode.Exclusive);
        readerWriter.Acquired[1].Mode.Should().Be(LockMode.Exclusive);
        readerWriter.Acquired[0].Key.Should().Contain(lower.Value.ToString("N"));
        readerWriter.Acquired[1].Key.Should().Contain(higher.Value.ToString("N"));
        readerWriter.Released.Should().HaveCount(2);
    }

    [Fact]
    public async Task Fails_with_descriptive_error_when_source_has_insufficient_funds()
    {
        var clock = new FixedClock();
        var repo = new FakeAccountRepo();
        var uow = new CountingUow();
        var distributed = new StubDistributedLock();
        var readerWriter = new RecordingReaderWriterLock();

        var from = NewActiveAccount(clock, opening: 10m);
        var to = NewActiveAccount(clock, opening: 0m);
        repo.Store[from.Id] = from;
        repo.Store[to.Id] = to;

        var sut = new TransferMoneyHandler(repo, uow, distributed, readerWriter, clock);

        var cmd = new TransferMoneyCommand(
            From: from.Id,
            To: to.Id,
            Amount: new Money(50m, Currency.TRY),
            IdempotencyKey: new IdempotencyKey("tx-2"));

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Insufficient funds");
        uow.Calls.Should().Be(0);
        from.Balance.Amount.Should().Be(10m);
        to.Balance.Amount.Should().Be(0m);
        // Both lock layers are released on the error path too.
        distributed.Released.Should().HaveCount(2);
        readerWriter.Released.Should().HaveCount(2);
    }
}
