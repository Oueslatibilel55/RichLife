using Microsoft.EntityFrameworkCore;
using RichLife.Application.Interfaces;
using RichLife.Domain.Entities;
using RichLife.Infrastructure.Persistence;

namespace RichLife.Infrastructure.Repositories;

public class CompanyRepository(GameDbContext db) : ICompanyRepository
{
    public Task<Company?> GetByPlayerIdAsync(Guid playerId, CancellationToken ct = default)
        => db.Companies
             .Include(c => c.Businesses).ThenInclude(b => b.Assets)
             .Include(c => c.Assets)
             .Include(c => c.LuxuryAssets)
             .Include(c => c.Loans)
             .AsSplitQuery()
             .FirstOrDefaultAsync(c => c.PlayerId == playerId, ct);

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Companies
             .Include(c => c.Businesses).ThenInclude(b => b.Assets)
             .Include(c => c.Assets)
             .Include(c => c.LuxuryAssets)
             .Include(c => c.Loans)
             .AsSplitQuery()
             .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddAsync(Company company, CancellationToken ct = default)
        => await db.Companies.AddAsync(company, ct);

    /// <summary>
    /// No-op for an aggregate loaded through this repository: it is already tracked, and
    /// the change tracker picks up mutations — including businesses and assets added to
    /// or removed from the aggregate. Kept so call sites read as explicit intent.
    /// </summary>
    public void Update(Company company)
    {
        if (db.Entry(company).State == EntityState.Detached)
            db.Companies.Update(company);
    }
}
