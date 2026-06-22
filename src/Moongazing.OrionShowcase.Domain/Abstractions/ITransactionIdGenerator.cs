namespace Moongazing.OrionShowcase.Domain.Abstractions;

using Moongazing.OrionShowcase.Domain.ValueObjects;

/// <summary>
/// Supplies a unique <see cref="TransactionId"/> for each ledger entry the domain creates.
/// Injected into <c>Account.Deposit</c>/<c>Account.Withdraw</c> the same way <see cref="IClock"/>
/// is, so the aggregate stays free of any infrastructure (id-generation) dependency. The
/// production implementation is backed by OrionKey's Snowflake generator; tests supply a
/// deterministic monotonic stub. Ids must be distinct across calls so two transactions written
/// in a single transfer never collide on the primary key.
/// </summary>
public interface ITransactionIdGenerator
{
    /// <summary>Returns a new unique transaction id.</summary>
    TransactionId NewId();
}
