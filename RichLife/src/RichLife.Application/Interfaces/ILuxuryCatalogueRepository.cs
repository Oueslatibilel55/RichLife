using RichLife.Domain.Catalogue;

namespace RichLife.Application.Interfaces;

public interface ILuxuryCatalogueRepository
{
    /// <summary>Active items, by required prestige then price.</summary>
    Task<IReadOnlyList<LuxuryCatalogueEntry>> GetActiveAsync(CancellationToken ct = default);

    Task<LuxuryCatalogueEntry?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>Every item, retired ones included (admin).</summary>
    Task<IReadOnlyList<LuxuryCatalogueEntry>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Tracked, for an admin edit.</summary>
    Task<LuxuryCatalogueEntry?> GetForUpdateAsync(string id, CancellationToken ct = default);

    Task AddAsync(LuxuryCatalogueEntry entry, CancellationToken ct = default);
}
