using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RichLife.Application.Interfaces;
using RichLife.Domain.Common;

namespace RichLife.Infrastructure.Events;

/// <summary>
/// In-process dispatcher. Resolves every <see cref="IDomainEventHandler{TEvent}"/>
/// registered for the concrete event type and invokes it.
/// </summary>
public class DomainEventDispatcher(
    IServiceProvider provider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, (Type HandlerType, MethodInfo Handle)> Cache = new();

    public async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var domainEvent in events)
        {
            var (handlerType, handle) = Cache.GetOrAdd(domainEvent.GetType(), eventType =>
            {
                var closed = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
                return (closed, closed.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!);
            });

            foreach (var handler in provider.GetServices(handlerType))
            {
                if (handler is null) continue;

                try
                {
                    await (Task)handle.Invoke(handler, [domainEvent, ct])!;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The originating change is already committed; a failing side effect
                    // must not surface as a failed player action.
                    logger.LogError(ex,
                        "Domain event handler {Handler} failed for {EventType}",
                        handler.GetType().Name, domainEvent.GetType().Name);
                }
            }
        }
    }
}
