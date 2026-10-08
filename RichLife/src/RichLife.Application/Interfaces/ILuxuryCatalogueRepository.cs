using RichLife.Domain.Catalogue;

namespace RichLife.Application.Interfaces;

public interface ILuxuryCatalogueRepository
{
    /// <summary>Active items, by required prestige then price.</summary>
    Task<IReadOnlyList<LuxuryCatalogueEntry>> GetActiveAsync(CancellationToken ct = default);

    Task<LuxuryCatalogueEntry?> GetByIdAsync(string id, CancellationToken ct = default);
}
