using RichLife.Domain.Common;

namespace RichLife.Application.Interfaces;

/// <summary>
/// Handles a domain event after the originating change has been committed.
/// Register implementations in DI; <c>IDomainEventDispatcher</c> resolves and invokes them.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}
