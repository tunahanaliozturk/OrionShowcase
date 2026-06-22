namespace Moongazing.OrionShowcase.Application.Tests.Accounts;

using System.Threading;
using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.ValueObjects;

/// <summary>
/// Deterministic <see cref="ITransactionIdGenerator"/> for handler/saga tests: returns 1, 2, 3, ...
/// so every ledger entry gets a distinct id (mirroring the production Snowflake generator's
/// uniqueness guarantee) without standing up OrionKey. Thread-safe so a test exercising concurrent
/// operations still gets unique ids.
/// </summary>
internal sealed class StubTransactionIdGenerator : ITransactionIdGenerator
{
    private long _next;

    public TransactionId NewId() => new(Interlocked.Increment(ref _next));
}
