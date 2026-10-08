using RichLife.Domain.Catalogue;

namespace RichLife.Application.Interfaces;

public interface IManagerNameRepository
{
    /// <summary>
    /// A random name, avoiding <paramref name="excludeIds"/> (names the company already
    /// uses) while any other is left. Null only if the table is empty.
    /// </summary>
    Task<ManagerName?> PickRandomAsync(IReadOnlyCollection<int> excludeIds, CancellationToken ct = default);

    // Admin
    Task<ManagerName?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, CancellationToken ct = default);
    Task<bool> IsInUseAsync(int id, CancellationToken ct = default);
    Task AddAsync(ManagerName name, CancellationToken ct = default);
    void Remove(ManagerName name);
}
