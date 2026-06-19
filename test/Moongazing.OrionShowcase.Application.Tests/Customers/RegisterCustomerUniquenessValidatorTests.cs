namespace Moongazing.OrionShowcase.Application.Tests.Customers;

using FluentAssertions;
using Moongazing.OrionShowcase.Application.Customers.Commands.RegisterCustomer;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;
using Xunit;

/// <summary>
/// Feature A: OrionGuard 6.6 asynchronous validation. The uniqueness rule runs a database-style
/// existence check inside the async validation pipeline and aggregates into the same GuardResult
/// as the synchronous rules.
/// </summary>
public class RegisterCustomerUniquenessValidatorTests
{
    private const string ValidTckn = "10000000146";

    private sealed class StubCustomerRepo : ICustomerRepository
    {
        private readonly HashSet<string> _existingNationalIds;

        public StubCustomerRepo(params string[] existingNationalIds)
            => _existingNationalIds = new HashSet<string>(existingNationalIds, StringComparer.Ordinal);

        public int ExistsCalls { get; private set; }

        public Task<bool> ExistsByNationalIdAsync(Tckn nationalId, CancellationToken cancellationToken)
        {
            ExistsCalls++;
            return Task.FromResult(_existingNationalIds.Contains(nationalId.Value));
        }

        public Task<Customer?> FindByNationalIdAsync(Tckn nationalId, CancellationToken cancellationToken)
            => Task.FromResult<Customer?>(null);

        public Task AddAsync(Customer customer, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Customer?> GetAsync(CustomerId id, CancellationToken cancellationToken)
            => Task.FromResult<Customer?>(null);
    }

    private static RegisterCustomerCommand Command() => new(
        FullName: "Ada Lovelace",
        NationalId: ValidTckn,
        Email: "ada@example.com",
        Phone: "+905551112233",
        IdempotencyKey: new IdempotencyKey("reg-1"));

    [Fact]
    public async Task Accepts_a_unique_national_id()
    {
        var repo = new StubCustomerRepo();
        var sut = new RegisterCustomerUniquenessValidator(repo);

        var result = await sut.ValidateAsync(Command(), CancellationToken.None);

        result.IsValid.Should().BeTrue();
        repo.ExistsCalls.Should().Be(1, "the async I/O-bound rule must actually run inside the pipeline");
    }

    [Fact]
    public async Task Rejects_a_duplicate_national_id_with_the_expected_code()
    {
        var repo = new StubCustomerRepo(ValidTckn);
        var sut = new RegisterCustomerUniquenessValidator(repo);

        var result = await sut.ValidateAsync(Command(), CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorCode.Should().Be("NATIONAL_ID_TAKEN");
    }

    [Fact]
    public void Synchronous_Validate_throws_because_rules_are_async()
    {
        var sut = new RegisterCustomerUniquenessValidator(new StubCustomerRepo());

        var act = () => sut.Validate(Command());

        act.Should().Throw<InvalidOperationException>(
            "a validator with async rules must not silently skip the database check on the sync path");
    }
}
