using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Domain;
using RichLife.Domain.Common;
using RichLife.Domain.Entities;
using RichLife.Domain.Store;

namespace RichLife.Application.Services;

/// <summary>The store: diamonds, boosts, the offline double, badges (contract §6e). The rules are on Company.</summary>
public class StoreService(
    ICompanyRepository companyRepo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    private const int HistorySize = 20;

    private DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public async Task<Result<StoreDto>> GetAsync(Guid playerId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<StoreDto>("Company not found.");
        return Result.Ok(await ToDtoAsync(company, UtcNow, ct));
    }

    public Task<Result<StoreDto>> BuyBoostAsync(Guid playerId, int hours, CancellationToken ct = default)
        => RunAsync(playerId, (c, now) => c.BuyBoost(hours, now), ct);

    public Task<Result<StoreDto>> DoubleOfflineAsync(Guid playerId, CancellationToken ct = default)
        => RunAsync(playerId, (c, now) => c.DoubleOfflineEarnings(now), ct);

    public Task<Result<StoreDto>> ExchangeAsync(Guid playerId, int diamonds, CancellationToken ct = default)
        => RunAsync(playerId, (c, now) => c.ExchangeDiamonds(diamonds, now), ct);

    public Task<Result<StoreDto>> BuyBadgeAsync(Guid playerId, string badgeId, CancellationToken ct = default)
        => RunAsync(playerId, (c, now) => BadgeCatalog.Find(badgeId) is { } badge
            ? c.BuyBadge(badge, now)
            : Result.Fail("Badge not found."), ct);

    public Task<Result<StoreDto>> FeatureBadgeAsync(Guid playerId, string? badgeId, CancellationToken ct = default)
        => RunAsync(playerId, (c, _) => c.FeatureBadge(string.IsNullOrWhiteSpace(badgeId) ? null : badgeId), ct);

    private async Task<Result<StoreDto>> RunAsync(
        Guid playerId, Func<Company, DateTime, Result> action, CancellationToken ct)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<StoreDto>("Company not found.");

        var now = UtcNow;
        var result = action(company, now);
        if (!result.IsSuccess) return Result.Fail<StoreDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);
        return Result.Ok(await ToDtoAsync(company, now, ct));
    }

    private async Task<StoreDto> ToDtoAsync(Company c, DateTime now, CancellationToken ct)
    {
        var owned = c.Badges.Select(b => b.BadgeId).ToHashSet();
        var history = await companyRepo.GetDiamondHistoryAsync(c.Id, HistorySize, ct);

        return new StoreDto(
            c.Diamonds,
            c.Cash,
            c.BoostUntil,
            GameConstants.BoostMultiplier,
            (int)GameConstants.MaxBoostAhead.TotalHours,
            GameConstants.BoostOptions.Select(o => new BoostOptionDto(o.Hours, o.Price)).ToList(),
            DoubleOffer(c, now),
            GameConstants.DiamondCashValue(c.PrestigeLevel),
            BadgeCatalog.All
                .Select(b => new StoreBadgeDto(b.Id, b.Icon, b.Name, b.Price, b.Rarity,
                    owned.Contains(b.Id), b.Id == c.FeaturedBadgeId))
                .ToList(),
            c.FeaturedBadgeId,
            history.Select(h => new DiamondTransactionDto(h.Amount, h.Balance, h.Reason, h.Detail, h.CreatedAt)).ToList());
    }

    /// <summary>Shared with <c>/state</c>, which announces the offer in the welcome-back dialog.</summary>
    public static OfflineDoubleOfferDto? DoubleOffer(Company c, DateTime now) =>
        c.HasOfflineDoubleAt(now)
            ? new OfflineDoubleOfferDto(c.OfflineBonusAmount, GameConstants.OfflineDoublePrice, c.OfflineBonusUntil!.Value)
            : null;

    public static IReadOnlyList<OwnedBadgeDto> OwnedBadges(Company c) =>
        c.Badges
            .OrderByDescending(b => b.PurchasedAt)
            .Select(b => (Owned: b, Def: BadgeCatalog.Find(b.BadgeId)))
            .Where(x => x.Def is not null)
            .Select(x => new OwnedBadgeDto(x.Def!.Id, x.Def.Icon, x.Def.Name, x.Def.Rarity, x.Owned.PurchasedAt))
            .ToList();
}
