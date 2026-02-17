using CRMS.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CRMS.Infrastructure.Persistence;

/// <summary>
/// EF Core interceptor that dispatches domain events after SaveChanges completes.
/// Events are collected from all tracked aggregates and dispatched in order.
/// Uses IServiceProvider to resolve the dispatcher at runtime to avoid circular dependencies.
/// </summary>
public class DomainEventPublishingInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider _serviceProvider;

    public DomainEventPublishingInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null)
        {
            DispatchDomainEventsAsync(eventData.Context, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        return result;
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken ct)
    {
        var aggregatesWithEvents = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = aggregatesWithEvents
            .SelectMany(a => a.DomainEvents)
            .OrderBy(e => e.OccurredAt)
            .ToList();

        if (domainEvents.Count == 0)
            return;

        // Clear events from aggregates before dispatching to prevent re-processing
        foreach (var aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }

        // Resolve dispatcher from a new scope to avoid the circular dependency
        using var scope = _serviceProvider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        
        // Dispatch all events
        await dispatcher.DispatchEventsAsync(domainEvents, ct);
    }
}
