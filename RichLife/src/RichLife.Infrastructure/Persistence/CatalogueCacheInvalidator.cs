using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using RichLife.Domain.Catalogue;
using RichLife.Infrastructure.Repositories;

namespace RichLife.Infrastructure.Persistence;

/// <summary>
/// Evicts the catalogue cache whenever a save touches a catalogue row. Lives on the
/// DbContext rather than in the admin service so no write path can forget it — the same
/// reasoning as dispatching domain events from the unit of work.
/// </summary>
/// <remarks>
/// Scoped, one per DbContext: the flag set while saving is read once the save succeeded.
/// Evicting only <i>after</i> the commit means a concurrent read cannot repopulate the
/// cache with the old rows in between.
/// </remarks>
public sealed class CatalogueCacheInvalidator(IMemoryCache cache) : SaveChangesInterceptor
{
    private bool _catalogueChanged;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        _catalogueChanged = eventData.Context?.ChangeTracker.Entries()
            .Any(e => e.Entity is BusinessCatalogueEntry or AssetCatalogueEntry
                      && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            ?? false;

        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
    {
        if (_catalogueChanged)
        {
            cache.Remove(CatalogueRepository.CacheKey);
            _catalogueChanged = false;
        }

        return base.SavedChangesAsync(eventData, result, ct);
    }
}
