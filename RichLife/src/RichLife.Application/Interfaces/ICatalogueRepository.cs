using RichLife.Domain.Catalogue;

namespace RichLife.Application.Interfaces;

public interface ICatalogueRepository
{
    /// <summary>
    /// Every entry, retired ones included, ordered for display. Served from a cache and
    /// <b>not tracked</b> — read-only. Use <see cref="GetForUpdateAsync"/> to edit.
    /// </summary>
    Task<IReadOnlyList<BusinessCatalogueEntry>> GetAllAsync(CancellationToken ct = default);

    /// <summary>One entry, retired or not, from the same untracked cache.</summary>
    Task<BusinessCatalogueEntry?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>A tracked entry, read from the database, for an edit committed by the unit of work.</summary>
    Task<BusinessCatalogueEntry?> GetForUpdateAsync(string id, CancellationToken ct = default);

    Task AddAsync(BusinessCatalogueEntry entry, CancellationToken ct = default);
}
