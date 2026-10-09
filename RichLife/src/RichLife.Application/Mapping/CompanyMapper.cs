using RichLife.Application.DTOs;
using RichLife.Domain.Entities;

namespace RichLife.Application.Mapping;

/// <summary>Single place that turns domain entities into API contracts.</summary>
/// <remarks>
/// Takes the current time because manager shifts expire: <c>isAutomated</c> and
/// <c>offlineIncomePerSecond</c> are "as of the response", not stored flags.
/// </remarks>
public static class CompanyMapper
{
    public static BusinessDto ToDto(Business b, DateTime nowUtc) => new(
        b.Id,
        b.CatalogueId,
        b.Name,
        b.Sector.ToString(),
        b.RequiredPrestige.ToString(),
        b.OpeningCost,
        b.NetIncomePerSecond,
        b.TotalValue,
        b.HasManagerAt(nowUtc),
        b.IsForSale,
        b.AskingPrice,
        b.Assets.Count,
        b.ManagerCost,
        b.ManagerName,
        b.ManagerUntil,
        b.Level,
        b.LevelMultiplier,
        b.NextLevelCost,
        b.NextLevelIncomePerSecond);

    public static CompanyDto ToDto(Company c, DateTime nowUtc) => new(
        c.Id,
        c.Name,
        c.Cash,
        c.PassiveIncomePerSecond,
        c.IncomePerSecond,
        c.OfflineIncomePerSecondAt(nowUtc),
        c.NetWorth,
        c.AllTimeEarnings,
        c.PrestigeLevel,
        c.PrestigeCount,
        c.PrestigeMultiplier,
        c.GetPrestigeThreshold(),
        c.LastSyncAt,
        c.Diamonds,
        c.BoostUntil,
        Domain.GameConstants.BoostMultiplier,
        Services.StoreService.Avatar(c.AvatarId),
        c.Businesses.Select(b => ToDto(b, nowUtc)).ToList());
}
