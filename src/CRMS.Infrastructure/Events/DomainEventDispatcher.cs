using CRMS.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.Events;

/// <summary>
/// Dispatches domain events to all registered handlers.
/// Uses IServiceProvider to resolve handlers at runtime.
/// </summary>
public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var domainEvent in events)
        {
            await DispatchEventAsync(domainEvent, ct);
        }
    }

    public async Task DispatchEventAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        _logger.LogDebug("Dispatching domain event {EventType} (ID: {EventId})", 
            eventType.Name, domainEvent.EventId);

        var handlers = _serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            try
            {
                var method = handlerType.GetMethod("HandleAsync");
                if (method != null)
                {
                    var task = (Task)method.Invoke(handler, new object[] { domainEvent, ct })!;
                    await task;
                }

                _logger.LogDebug("Domain event {EventType} handled by {HandlerType}", 
                    eventType.Name, handler?.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling domain event {EventType} in {HandlerType}", 
                    eventType.Name, handler?.GetType().Name);
                // Continue processing other handlers - don't fail the whole transaction
            }
        }
    }
}
