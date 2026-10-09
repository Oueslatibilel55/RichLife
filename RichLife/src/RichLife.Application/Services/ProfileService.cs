using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Domain.Achievements;
using RichLife.Domain.Common;
using RichLife.Domain.Entities;

namespace RichLife.Application.Services;

/// <summary>The player's own profile page: account, company summary, rank, achievements.</summary>
public class ProfileService(
    IPlayerRepository playerRepo,
    ICompanyRepository companyRepo,
    ILeaderboardRepository leaderboardRepo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    public async Task<Result<ProfileDto>> GetAsync(Guid playerId, CancellationToken ct = default)
    {
        var player = await playerRepo.GetByIdAsync(playerId, ct);
        if (player is null) return Result.Fail<ProfileDto>("Player not found.");

        var now = clock.GetUtcNow().UtcDateTime;
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);

        // Catch anything met since the last sync, so the page never shows a met goal as locked.
        if (company is not null && company.UnlockAchievements(now).Count > 0)
        {
            companyRepo.Update(company);
            await uow.CommitAsync(ct);
        }

        // No company → unranked, but still say how many players are ranked.
        var (rank, total) = await leaderboardRepo.GetRankAsync(company?.AllTimeEarnings ?? decimal.MaxValue, ct);

        var achievements = AchievementCatalog.All.Select(a => ToDto(a, company)).ToList();

        return Result.Ok(new ProfileDto(
            player.Username,
            player.Email,
            player.Country,
            player.CreatedAt,
            company is null ? null : rank,
            total,
            company is null ? null : ToDto(company, now),
            company is null ? [] : company.LuxuryAssets.OrderByDescending(l => l.CreatedAt).Select(LuxuryService.ToDto).ToList(),
            achievements.Count(a => a.Unlocked),
            achievements.Count,
            achievements,
            company is null ? [] : StoreService.OwnedBadges(company),
            company?.FeaturedBadgeId,
            StoreService.Avatar(company?.AvatarId)));
    }

    private static ProfileCompanyDto ToDto(Company c, DateTime now) => new(
        c.Name,
        c.CreatedAt,
        c.PrestigeLevel.ToString(),
        c.PrestigeCount,
        c.PrestigeMultiplier,
        c.Cash,
        c.NetWorth,
        c.AllTimeEarnings,
        c.IncomePerSecond,
        c.Businesses.Count,
        (int)c.AchievementMetricValue(AchievementMetric.AssetsOwned),
        c.Businesses.Count(b => b.HasManagerAt(now)),
        (int)c.AchievementMetricValue(AchievementMetric.ManagersHired),
        (int)c.AchievementMetricValue(AchievementMetric.HighestBusinessLevel),
        c.Diamonds);

    private static AchievementDto ToDto(AchievementDefinition a, Company? c)
    {
        var record = c?.Achievements.FirstOrDefault(x => x.Code == a.Code);
        var current = c?.AchievementMetricValue(a.Metric) ?? 0m;
        // Once unlocked it stays full, even if the metric dropped (a business was closed).
        if (record is not null) current = a.Target;
        return new AchievementDto(
            a.Code, a.Title, a.Description, a.Icon,
            Unlocked: record is not null,
            UnlockedAt: record?.UnlockedAt,
            Current: Math.Min(current, a.Target),
            Target: a.Target,
            Unit: a.Metric is AchievementMetric.AllTimeEarnings or AchievementMetric.CashOnHand ? "money" : "count");
    }
}
