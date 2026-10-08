using Microsoft.EntityFrameworkCore;
using RichLife.Application.Interfaces;
using RichLife.Domain.Common;

namespace RichLife.Infrastructure.Persistence;

public class UnitOfWork(GameDbContext db, IDomainEventDispatcher dispatcher) : IUnitOfWork
{
    /// <summary>
    /// Saves the change and then dispatches whatever domain events the tracked
    /// aggregates raised. Doing it here means every command path publishes its
    /// events — services cannot forget to.
    /// </summary>
    public async Task<int> CommitAsync(CancellationToken ct = default)
    {
        var roots = db.ChangeTracker
            .Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var events = roots.SelectMany(r => r.DomainEvents).ToList();

        var affected = await db.SaveChangesAsync(ct);

        if (events.Count > 0)
        {
            // Cleared before dispatch so a handler that commits again cannot
            // republish the same events.
            foreach (var root in roots) root.ClearDomainEvents();
            await dispatcher.DispatchAsync(events, ct);
        }

        return affected;
    }
}
