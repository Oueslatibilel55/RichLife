using Microsoft.EntityFrameworkCore;
using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Domain.Achievements;
using RichLife.Domain.Enums;
using RichLife.Infrastructure.Persistence;

namespace RichLife.Infrastructure.Repositories;

public class AdminReadRepository(GameDbContext db) : IAdminReadRepository
{
    public async Task<AdminStatsDto> GetStatsAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var dayAgo = nowUtc.AddDays(-1);
        var weekAgo = nowUtc.AddDays(-7);

        // Admins are staff, not players: every player figure leaves them out. A promoted
        // player keeps their old company in the database, so filter by owner, not by
        // "has a company".
        var gamers = db.Players.Where(p => !p.IsAdmin);
        var companiesQ = gamers.Where(p => p.Company != null).Select(p => p.Company!);
        var companyIds = companiesQ.Select(c => c.Id);
        var businessesQ = db.Businesses.Where(b => companyIds.Contains(b.CompanyId));

        // Sequential on purpose: a DbContext runs one query at a time. Admin-only, cheap enough.
        var players = await gamers.CountAsync(ct);
        var admins = await db.Players.CountAsync(p => p.IsAdmin, ct);
        var new24h = await gamers.CountAsync(p => p.CreatedAt >= dayAgo, ct);
        var new7d = await gamers.CountAsync(p => p.CreatedAt >= weekAgo, ct);

        var companies = await companiesQ.CountAsync(ct);
        var active24h = await companiesQ.CountAsync(c => c.LastSyncAt >= dayAgo, ct);
        var totalCash = await companiesQ.SumAsync(c => (decimal?)c.Cash, ct) ?? 0m;
        var totalEarnings = await companiesQ.SumAsync(c => (decimal?)c.AllTimeEarnings, ct) ?? 0m;

        var businesses = await businessesQ.CountAsync(ct);
        var onShift = await businessesQ.CountAsync(b => b.ManagerUntil > nowUtc, ct);
        var avgLevel = await businessesQ.AverageAsync(b => (decimal?)b.Level, ct) ?? 0m;

        var byLevel = await companiesQ
            .GroupBy(c => c.PrestigeLevel)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var distribution = Enum.GetValues<PrestigeLevel>()
            .Select(l => new PrestigeCountDto(l.ToString(), byLevel.FirstOrDefault(x => x.Level == l)?.Count ?? 0))
            .ToList();

        var top = await businessesQ
            .GroupBy(b => b.CatalogueId)
            .Select(g => new { CatalogueId = g.Key, Name = g.Max(b => b.Name)!, Owners = g.Count() })
            .OrderByDescending(x => x.Owners).ThenBy(x => x.CatalogueId)
            .Take(5)
            .ToListAsync(ct);

        var catalogue = await db.Catalogue.CountAsync(ct);
        var catalogueActive = await db.Catalogue.CountAsync(e => e.IsActive, ct);
        var catalogueAssets = await db.Catalogue.SelectMany(e => e.AvailableAssets).CountAsync(ct);
        var names = await db.ManagerNames.CountAsync(ct);

        var luxuryOwned = await db.LuxuryAssets.CountAsync(l => companyIds.Contains(l.CompanyId), ct);
        var luxuryCatalogue = await db.LuxuryCatalogue.CountAsync(ct);
        var luxuryActive = await db.LuxuryCatalogue.CountAsync(i => i.IsActive, ct);

        var unlocks = await companiesQ
            .SelectMany(c => c.Achievements)
            .GroupBy(a => a.Code)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var achievementDistribution = AchievementCatalog.All
            .Select(a => new AchievementCountDto(a.Code, a.Title, a.Icon,
                unlocks.FirstOrDefault(u => u.Code == a.Code)?.Count ?? 0))
            .ToList();

        // Outstanding is computed on the entity; spelled out here so it runs in SQL.
        var loansQ = db.Loans.Where(l => companyIds.Contains(l.CompanyId));
        var activeLoansQ = loansQ.Where(l => l.RepaidAt == null);
        var loansTaken = await loansQ.CountAsync(ct);
        var activeLoans = await activeLoansQ.CountAsync(ct);
        var outstanding = await activeLoansQ
            .SumAsync(l => (decimal?)(l.TotalRepay + l.Penalties - l.Paid - l.ForgivenAmount), ct) ?? 0m;
        var missed = await activeLoansQ.SumAsync(l => (int?)l.MissedPayments, ct) ?? 0;

        return new AdminStatsDto(
            nowUtc, players, admins, new24h, new7d, active24h,
            companies, totalCash, totalEarnings,
            businesses, onShift, Math.Round(avgLevel, 2),
            distribution,
            top.Select(t => new TopBusinessDto(t.CatalogueId, t.Name, t.Owners)).ToList(),
            catalogue, catalogueActive, catalogueAssets, names,
            luxuryOwned, luxuryCatalogue, luxuryActive,
            unlocks.Sum(u => u.Count), achievementDistribution,
            loansTaken, activeLoans, outstanding, missed);
    }

    public async Task<IReadOnlyList<AdminPlayerDto>> GetPlayersAsync(
        string? search, int take, CancellationToken ct = default)
    {
        var query = db.Players.AsNoTracking();
        if (search is not null)
        {
            var pattern = $"%{search}%";
            query = query.Where(p => EF.Functions.ILike(p.Username, pattern) || EF.Functions.ILike(p.Email, pattern));
        }

        var rows = await Project(query.OrderByDescending(p => p.CreatedAt).Take(take)).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<AdminPlayerDto?> GetPlayerAsync(Guid playerId, CancellationToken ct = default)
    {
        var row = await Project(db.Players.AsNoTracking().Where(p => p.Id == playerId)).FirstOrDefaultAsync(ct);
        return row is null ? null : ToDto(row);
    }

    public async Task<IReadOnlyList<ManagerNameDto>> GetManagerNamesAsync(CancellationToken ct = default)
        => await db.ManagerNames.AsNoTracking()
            .OrderBy(m => m.Name)
            .Select(m => new ManagerNameDto(m.Id, m.Name, db.Businesses.Count(b => b.ManagerNameId == m.Id)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AdminLoanDto>> GetLoansAsync(
        bool activeOnly, int take, CancellationToken ct = default)
    {
        var loans = db.Loans.AsNoTracking();
        if (activeOnly) loans = loans.Where(l => l.RepaidAt == null);

        return await (
            from l in loans
            join c in db.Companies on l.CompanyId equals c.Id
            join p in db.Players on c.PlayerId equals p.Id
            orderby l.CreatedAt descending
            select new AdminLoanDto(
                l.Id, p.Id, p.Username, c.Name, l.BankId, l.BankName, l.BankIcon,
                l.Principal, l.InterestRate, l.TotalRepay, l.Paid, l.Penalties,
                l.TotalRepay + l.Penalties - l.Paid - l.ForgivenAmount,
                l.MissedPayments, l.CreatedAt, l.NextPaymentAt, l.RepaidAt, l.ForgivenAmount > 0m))
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BankUsage>> GetBankUsageAsync(CancellationToken ct = default)
        => await db.Loans.AsNoTracking()
            .GroupBy(l => l.BankId)
            .Select(g => new BankUsage(
                g.Key, g.Count(), g.Count(l => l.RepaidAt == null), g.Sum(l => l.Principal)))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<string, int>> GetLuxuryOwnersAsync(CancellationToken ct = default)
        => await db.LuxuryAssets.AsNoTracking()
            .GroupBy(l => l.CatalogueId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

    // Enums are mapped to strings after the query, not inside it.
    private sealed record PlayerRow(
        Guid Id, string Username, string Email, string Country, bool IsAdmin, DateTime CreatedAt,
        string? CompanyName, decimal? Cash, PrestigeLevel? PrestigeLevel, int? PrestigeCount,
        decimal? AllTimeEarnings, int? Businesses, DateTime? LastSeenAt,
        int? HighestBusinessLevel, int? LuxuryOwned, int? AchievementsUnlocked, decimal? LoanOutstanding);

    private static IQueryable<PlayerRow> Project(IQueryable<Domain.Entities.Player> players) =>
        players.Select(p => new PlayerRow(
            p.Id, p.Username, p.Email, p.Country, p.IsAdmin, p.CreatedAt,
            p.Company == null ? null : p.Company.Name,
            p.Company == null ? null : p.Company.Cash,
            p.Company == null ? null : p.Company.PrestigeLevel,
            p.Company == null ? null : p.Company.PrestigeCount,
            p.Company == null ? null : p.Company.AllTimeEarnings,
            p.Company == null ? null : p.Company.Businesses.Count,
            p.Company == null ? null : p.Company.LastSyncAt,
            p.Company == null ? null : p.Company.Businesses.Max(b => (int?)b.Level),
            p.Company == null ? null : p.Company.LuxuryAssets.Count,
            p.Company == null ? null : p.Company.Achievements.Count,
            p.Company == null ? null : p.Company.Loans
                .Where(l => l.RepaidAt == null)
                .Select(l => (decimal?)(l.TotalRepay + l.Penalties - l.Paid - l.ForgivenAmount))
                .FirstOrDefault()));

    private static AdminPlayerDto ToDto(PlayerRow r) => new(
        r.Id, r.Username, r.Email, r.Country, r.IsAdmin, r.CreatedAt,
        r.CompanyName, r.Cash, r.PrestigeLevel?.ToString(), r.PrestigeCount,
        r.AllTimeEarnings, r.Businesses, r.LastSeenAt,
        r.HighestBusinessLevel, r.LuxuryOwned, r.AchievementsUnlocked, r.LoanOutstanding);
}
