namespace CRMS.Domain.Common;

/// <summary>
/// Dispatches domain events to registered handlers.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
    Task DispatchEventAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}

/// <summary>
/// Marker interface for domain event handlers.
/// </summary>
public interface IDomainEventHandler<TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}
