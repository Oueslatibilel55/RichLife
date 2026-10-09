using Microsoft.EntityFrameworkCore;
using RichLife.Application.Interfaces;
using RichLife.Domain.Catalogue;
using RichLife.Infrastructure.Persistence;

namespace RichLife.Infrastructure.Repositories;

/// <summary>Under a hundred rows of content, read untracked; small enough not to need a cache.</summary>
public class LuxuryCatalogueRepository(GameDbContext db) : ILuxuryCatalogueRepository
{
    public async Task<IReadOnlyList<LuxuryCatalogueEntry>> GetActiveAsync(CancellationToken ct = default)
        => await db.LuxuryCatalogue.AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.RequiredPrestige).ThenBy(i => i.DisplayOrder).ThenBy(i => i.Price)
            .ToListAsync(ct);

    public Task<LuxuryCatalogueEntry?> GetByIdAsync(string id, CancellationToken ct = default)
        => db.LuxuryCatalogue.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<LuxuryCatalogueEntry>> GetAllAsync(CancellationToken ct = default)
        => await db.LuxuryCatalogue.AsNoTracking()
            .OrderBy(i => i.RequiredPrestige).ThenBy(i => i.DisplayOrder).ThenBy(i => i.Price)
            .ToListAsync(ct);

    public Task<LuxuryCatalogueEntry?> GetForUpdateAsync(string id, CancellationToken ct = default)
        => db.LuxuryCatalogue.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task AddAsync(LuxuryCatalogueEntry entry, CancellationToken ct = default)
        => await db.LuxuryCatalogue.AddAsync(entry, ct);
}
