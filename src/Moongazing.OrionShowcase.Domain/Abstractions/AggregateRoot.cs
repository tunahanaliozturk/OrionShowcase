namespace Moongazing.OrionShowcase.Domain.Abstractions;

using System.Diagnostics.CodeAnalysis;

public abstract class AggregateRoot<TId>
    where TId : struct
{
    public TId Id { get; protected init; }
    private readonly List<object> _events = new();
    public IReadOnlyList<object> DomainEvents => _events;

    [SuppressMessage("Design", "CA1030:Use events where appropriate",
        Justification = "Domain events are collected for deferred dispatch by infrastructure, not CLR events.")]
    protected void Raise(object domainEvent) => _events.Add(domainEvent);

    public void ClearDomainEvents() => _events.Clear();
}
