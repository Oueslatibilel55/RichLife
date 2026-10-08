using Microsoft.EntityFrameworkCore;
using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Infrastructure.Persistence;

namespace RichLife.Infrastructure.Repositories;

public class LeaderboardRepository(GameDbContext db) : ILeaderboardRepository
{
    public async Task<(int Rank, int Total)> GetRankAsync(decimal allTimeEarnings, CancellationToken ct = default)
    {
        var ranked = db.Players.Where(p => p.Company != null && !p.IsAdmin);
        var ahead = await ranked.CountAsync(p => p.Company!.AllTimeEarnings > allTimeEarnings, ct);
        var total = await ranked.CountAsync(ct);
        return (ahead + 1, total);
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetTopByEarningsAsync(
        int take, CancellationToken ct = default)
    {
        var rows = await db.Players
            .AsNoTracking()
            .Where(p => p.Company != null && !p.IsAdmin) // admins are staff, not ranked
            .OrderByDescending(p => p.Company!.AllTimeEarnings)
            .Take(take)
            .Select(p => new
            {
                p.Username,
                p.Country,
                CompanyName = p.Company!.Name,
                p.Company.AllTimeEarnings,
                p.Company.PrestigeLevel,
                p.Company.PrestigeCount
            })
            .ToListAsync(ct);

        return rows
            .Select((r, i) => new LeaderboardEntryDto(
                Rank: i + 1,
                Username: r.Username,
                Country: r.Country,
                CompanyName: r.CompanyName,
                AllTimeEarnings: r.AllTimeEarnings,
                PrestigeLevel: r.PrestigeLevel,
                PrestigeCount: r.PrestigeCount))
            .ToList();
    }
}
