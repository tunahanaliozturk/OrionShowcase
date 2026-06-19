namespace Moongazing.OrionShowcase.Application.Tests.Customers;

using System.Text;
using FluentAssertions;
using Moongazing.OrionShowcase.Application.Abstractions;
using Moongazing.OrionShowcase.Application.Customers.Commands.RegisterCustomer;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

public class RegisterCustomerHandlerTests
{
    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
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

        public Task<bool> ExistsByNationalIdAsync(Tckn nationalId, CancellationToken cancellationToken)
            => Task.FromResult(Store.Values.Any(c => c.NationalId.Value == nationalId.Value));

        public Task<Customer?> FindByNationalIdAsync(Tckn nationalId, CancellationToken cancellationToken)
            => Task.FromResult(Store.Values.FirstOrDefault(c => c.NationalId.Value == nationalId.Value));
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

    // Deterministic stand-in for the OrionVault blind index: equal national ids yield equal bytes.
    private sealed class FakeIndexer : INationalIdIndexer
    {
        public byte[] Compute(Tckn nationalId) => Encoding.UTF8.GetBytes("idx:" + nationalId.Value);

        public IReadOnlyList<byte[]> ComputeAllVersions(Tckn nationalId) => new[] { Compute(nationalId) };
    }

    [Fact]
    public async Task Registers_customer_and_returns_new_id()
    {
        var repo = new FakeCustomerRepo();
        var uow = new CountingUow();
        var sut = new RegisterCustomerHandler(repo, uow, new FixedClock(), new FakeIndexer());

        var cmd = new RegisterCustomerCommand(
            FullName: "Ada Lovelace",
            NationalId: "10000000146",
            Email: "ada@example.com",
            Phone: "+905551112233",
            IdempotencyKey: new IdempotencyKey("reg-1"));

        var result = await sut.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CustomerId.Should().NotBe(Guid.Empty);
        repo.Store.Should().ContainSingle();
        uow.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Persists_customer_with_supplied_personal_data()
    {
        var repo = new FakeCustomerRepo();
        var uow = new CountingUow();
        var sut = new RegisterCustomerHandler(repo, uow, new FixedClock(), new FakeIndexer());

        var cmd = new RegisterCustomerCommand(
            FullName: "Grace Hopper",
            NationalId: "10000000146",
            Email: "grace@example.com",
            Phone: "+905554443322",
            IdempotencyKey: new IdempotencyKey("reg-2"));

        var result = await sut.Handle(cmd, CancellationToken.None);

        var stored = repo.Store.Values.Single();
        stored.FullName.Should().Be("Grace Hopper");
        stored.Email.Should().Be("grace@example.com");
        stored.Phone.Should().Be("+905554443322");
        stored.NationalId.Value.Should().Be("10000000146");
        stored.Id.Value.Should().Be(result.Value!.CustomerId);
    }
}
