namespace Moongazing.OrionShowcase.Infrastructure.Outbox;

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moongazing.OrionPatch.Abstractions;
using Moongazing.OrionShowcase.Domain.Abstractions;

/// <summary>
/// EF Core save-changes interceptor that bridges domain events raised on
/// <see cref="AggregateRoot{TId}"/> instances tracked by the bound DbContext to
/// OrionPatch's <see cref="IOutbox"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a scoped service alongside the scoped <see cref="IOutbox"/>. The
/// adapter must be attached to the DbContext options BEFORE
/// <see cref="Moongazing.OrionPatch.EntityFrameworkCore.OrionPatchSaveChangesInterceptor"/>
/// so that domain events are enqueued into <see cref="IOutbox"/> before OrionPatch
/// drains the per-context buffer into the change tracker during
/// <c>SavingChanges</c>/<c>SavingChangesAsync</c>.
/// </para>
/// <para>
/// On a successful save (<c>SavedChanges*</c>) the adapter clears the per-aggregate
/// <see cref="AggregateRoot{TId}.DomainEvents"/> list so the same events are not
/// re-enqueued on a subsequent <c>SaveChanges</c>.
/// </para>
/// </remarks>
public sealed class DomainEventOutboxAdapter : SaveChangesInterceptor
{
    private readonly IOutbox outbox;
    private readonly List<object> processedAggregates = new();

    private static readonly MethodInfo EnqueueMethod = typeof(IOutbox)
        .GetMethod(nameof(IOutbox.Enqueue))
        ?? throw new InvalidOperationException("IOutbox.Enqueue method not found.");

    public DomainEventOutboxAdapter(IOutbox outbox)
    {
        ArgumentNullException.ThrowIfNull(outbox);
        this.outbox = outbox;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Flush(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Flush(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        ClearProcessed();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        ClearProcessed();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        processedAggregates.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        processedAggregates.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Flush(DbContext? db)
    {
        if (db is null)
        {
            return;
        }

        processedAggregates.Clear();

        var aggregates = db.ChangeTracker.Entries()
            .Select(e => e.Entity)
            .Where(IsAggregateRoot)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            var events = GetDomainEvents(aggregate);
            if (events.Count == 0)
            {
                continue;
            }

            foreach (var domainEvent in events)
            {
                EnqueueDomainEvent(domainEvent);
            }

            processedAggregates.Add(aggregate);
        }
    }

    private void ClearProcessed()
    {
        foreach (var aggregate in processedAggregates)
        {
            ClearDomainEvents(aggregate);
        }
        processedAggregates.Clear();
    }

    private void EnqueueDomainEvent(object domainEvent)
    {
        // IOutbox.Enqueue<T>(T message, OutboxEnqueueOptions? options = null) where T : class
        // Bind T to the runtime type so OrionPatch's MessageTypeNameResolver records
        // the concrete event type rather than System.Object.
        var generic = EnqueueMethod.MakeGenericMethod(domainEvent.GetType());
        generic.Invoke(outbox, new object?[] { domainEvent, null });
    }

    private static IReadOnlyList<object> GetDomainEvents(object aggregate)
    {
        var property = aggregate.GetType().GetProperty(
            nameof(AggregateRoot<int>.DomainEvents))
            ?? throw new InvalidOperationException(
                $"Aggregate {aggregate.GetType().FullName} does not expose a DomainEvents property.");
        return (IReadOnlyList<object>)property.GetValue(aggregate)!;
    }

    private static void ClearDomainEvents(object aggregate)
    {
        var method = aggregate.GetType().GetMethod(
            nameof(AggregateRoot<int>.ClearDomainEvents))
            ?? throw new InvalidOperationException(
                $"Aggregate {aggregate.GetType().FullName} does not expose a ClearDomainEvents method.");
        method.Invoke(aggregate, null);
    }

    private static bool IsAggregateRoot(object entity)
    {
        var t = entity.GetType();
        while (t is not null && t != typeof(object))
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
            {
                return true;
            }
            t = t.BaseType;
        }
        return false;
    }
}
