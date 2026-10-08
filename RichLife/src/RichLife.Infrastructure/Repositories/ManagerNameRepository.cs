using Microsoft.EntityFrameworkCore;
using RichLife.Application.Interfaces;
using RichLife.Domain.Catalogue;
using RichLife.Infrastructure.Persistence;

namespace RichLife.Infrastructure.Repositories;

public class ManagerNameRepository(GameDbContext db) : IManagerNameRepository
{
    public async Task<ManagerName?> PickRandomAsync(
        IReadOnlyCollection<int> excludeIds, CancellationToken ct = default)
    {
        // ORDER BY RANDOM() LIMIT 1 — fine for a table of a few hundred rows.
        var unused = await db.ManagerNames.AsNoTracking()
            .Where(m => !excludeIds.Contains(m.Id))
            .OrderBy(_ => EF.Functions.Random())
            .FirstOrDefaultAsync(ct);

        // Every name already used by this company: allow a repeat rather than fail.
        return unused ?? await db.ManagerNames.AsNoTracking()
            .OrderBy(_ => EF.Functions.Random())
            .FirstOrDefaultAsync(ct);
    }

    public Task<ManagerName?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.ManagerNames.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<bool> NameExistsAsync(string name, CancellationToken ct = default)
        => db.ManagerNames.AnyAsync(m => m.Name.ToLower() == name.ToLower(), ct);

    public Task<bool> IsInUseAsync(int id, CancellationToken ct = default)
        => db.Businesses.AnyAsync(b => b.ManagerNameId == id, ct);

    public async Task AddAsync(ManagerName name, CancellationToken ct = default)
        => await db.ManagerNames.AddAsync(name, ct);

    public void Remove(ManagerName name) => db.ManagerNames.Remove(name);
}
