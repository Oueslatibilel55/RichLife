using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RichLife.Application.Interfaces;
using RichLife.Domain.Catalogue;
using RichLife.Infrastructure.Persistence;

namespace RichLife.Infrastructure.Repositories;

/// <summary>
/// The catalogue is read on every catalogue view, open and asset purchase, but changes only
/// when an admin edits it — so the whole thing is cached as one untracked snapshot.
/// <see cref="CatalogueCacheInvalidator"/> evicts it after any committed edit.
/// </summary>
public class CatalogueRepository(GameDbContext db, IMemoryCache cache) : ICatalogueRepository
{
    internal const string CacheKey = "catalogue:snapshot";

    // Safety net only — eviction on write is what keeps the cache correct. This bounds
    // staleness if a row is ever edited by hand in SQL, or by a second API instance.
    private static readonly TimeSpan MaxStaleness = TimeSpan.FromMinutes(10);

    public async Task<IReadOnlyList<BusinessCatalogueEntry>> GetAllAsync(CancellationToken ct = default)
        => (await GetSnapshotAsync(ct)).Ordered;

    public async Task<BusinessCatalogueEntry?> GetByIdAsync(string id, CancellationToken ct = default)
        => (await GetSnapshotAsync(ct)).ById.GetValueOrDefault(id);

    public Task<BusinessCatalogueEntry?> GetForUpdateAsync(string id, CancellationToken ct = default)
        => db.Catalogue.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task AddAsync(BusinessCatalogueEntry entry, CancellationToken ct = default)
        => await db.Catalogue.AddAsync(entry, ct);

    private async Task<Snapshot> GetSnapshotAsync(CancellationToken ct)
        => (await cache.GetOrCreateAsync(CacheKey, async item =>
        {
            item.AbsoluteExpirationRelativeToNow = MaxStaleness;

            var entries = await db.Catalogue
                .AsNoTracking()
                .OrderBy(e => e.RequiredPrestige)
                .ThenBy(e => e.DisplayOrder)
                .ThenBy(e => e.Id)
                .ToListAsync(ct);

            return new Snapshot(entries, entries.ToDictionary(e => e.Id, StringComparer.Ordinal));
        }))!;

    private sealed record Snapshot(
        IReadOnlyList<BusinessCatalogueEntry> Ordered,
        IReadOnlyDictionary<string, BusinessCatalogueEntry> ById);
}
