namespace Moongazing.OrionShowcase.Application.Customers.Commands.RegisterCustomer;

using Moongazing.OrionGuard.Core;
using Moongazing.OrionGuard.DependencyInjection;
using Moongazing.OrionShowcase.Domain.Repositories;
using Moongazing.OrionShowcase.Domain.ValueObjects;

/// <summary>
/// OrionGuard 6.6 asynchronous validator for <see cref="RegisterCustomerCommand"/>.
/// </summary>
/// <remarks>
/// Uniqueness of the national id needs a database round-trip, so the rule cannot run on the
/// synchronous validation path. This validator expresses it with OrionGuard's async pipeline:
/// <c>Validate.For(command).MustAsync(selector, predicate, message, code).ToResultAsync(ct)</c>.
/// The deferred I/O-bound rule executes inside the awaited <c>ToResultAsync</c>, in the same
/// failure-aggregation pass as any synchronous rules, and the resulting errors flow back through
/// the existing <c>ValidationBehavior</c> exactly like the structural <see cref="RegisterCustomerValidator"/>.
/// The lookup is satisfied by the OrionVault deterministic blind index, so it resolves as a single
/// indexed equality seek and never decrypts a row.
/// Registered automatically by the assembly scan in <c>AddApplication</c>.
/// </remarks>
public sealed class RegisterCustomerUniquenessValidator : IValidator<RegisterCustomerCommand>
{
    private readonly ICustomerRepository _customers;

    public RegisterCustomerUniquenessValidator(ICustomerRepository customers)
    {
        ArgumentNullException.ThrowIfNull(customers);
        _customers = customers;
    }

    public async Task<GuardResult> ValidateAsync(
        RegisterCustomerCommand value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        return await Moongazing.OrionGuard.Core.Validate.For(value)
            .MustAsync(
                x => x.NationalId,
                async (nationalId, ct) =>
                    !await _customers.ExistsByNationalIdAsync(new Tckn(nationalId), ct).ConfigureAwait(false),
                "A customer with this national id already exists.",
                "NATIONAL_ID_TAKEN")
            .ToResultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<GuardResult> ValidateAsync(
        RegisterCustomerCommand value,
        ValidationContext context,
        CancellationToken cancellationToken)
        => ValidateAsync(value, cancellationToken);

    // Synchronous entry points cannot honour the async uniqueness rules. Mirror OrionGuard's own
    // ToResult() contract and fail loudly rather than silently skipping the database checks.
    public GuardResult Validate(RegisterCustomerCommand value)
        => throw new InvalidOperationException(
            "RegisterCustomerUniquenessValidator declares asynchronous rules; call ValidateAsync instead.");

    public GuardResult Validate(RegisterCustomerCommand value, ValidationContext context)
        => this.Validate(value);
}
