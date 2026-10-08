using RichLife.Domain.Entities;

namespace RichLife.Application.Interfaces;

public interface ICompanyRepository
{
    Task<Company?> GetByPlayerIdAsync(Guid playerId, CancellationToken ct = default);
    Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Company company, CancellationToken ct = default);
    void Update(Company company);
}
