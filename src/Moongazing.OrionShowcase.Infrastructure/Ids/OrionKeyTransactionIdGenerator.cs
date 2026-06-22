namespace Moongazing.OrionShowcase.Infrastructure.Ids;

using Moongazing.OrionShowcase.Domain.Abstractions;
using Moongazing.OrionShowcase.Domain.ValueObjects;

/// <summary>
/// <see cref="ITransactionIdGenerator"/> backed by OrionKey's process-wide Snowflake generator.
/// <para>
/// Snowflake ids are 64-bit, strictly increasing per process, and thread-safe, which makes them a
/// natural fit for the <c>bigint</c> <c>transactions.id</c> primary key (mapped
/// <c>ValueGeneratedNever</c>): the domain assigns the id at creation time, so EF Core tracks each
/// transaction under a distinct key and never has to reconcile two sentinel-keyed entities in one
/// <c>SaveChanges</c>. The worker id is configured once at startup in <c>AddInfrastructure</c>.
/// </para>
/// </summary>
public sealed class OrionKeyTransactionIdGenerator : ITransactionIdGenerator
{
    public TransactionId NewId() => new(Moongazing.OrionKey.OrionKey.NextSnowflake());
}
