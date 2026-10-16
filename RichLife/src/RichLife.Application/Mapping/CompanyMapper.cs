using RichLife.Application.DTOs;
using RichLife.Domain;
using RichLife.Domain.Entities;

namespace RichLife.Application.Mapping;

/// <summary>Single place that turns domain entities into API contracts.</summary>
/// <remarks>
/// Takes the current time because manager shifts expire and units break: <c>isAutomated</c>,
/// <c>offlineIncomePerSecond</c>, the incomes and each unit's <c>status</c> are "as of the
/// response", not stored flags.
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
        b.NetIncomePerSecondAt(nowUtc),
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
        b.NextLevelIncomePerSecondAt(nowUtc),
        b.TaxDue,
        b.TaxAccruing,
        b.TaxPeriodEndsAt,
        b.BrokenUnitsAt(nowUtc),
        GameConstants.MaxPlacesPerBusiness,
        b.Places.Select(p => ToDto(b, p, nowUtc)).ToList(),
        b.Kind.ToString());

    private static PlaceDto ToDto(Business b, BusinessPlace p, DateTime nowUtc) => new(
        p.Id,
        p.KindId,
        p.Name,
        p.Street,
        p.Tier.ToString(),
        p.Level,
        GameConstants.MaxPlaceLevel,
        p.Multiplier,
        p.Capacity,
        b.UnitCountIn(p),
        p.BaseIncomePerSecond,
        b.PlaceIncomePerSecondAt(p, nowUtc),
        p.NextLevelCost,
        p.IsMaxLevel ? null : p.CapacityAt(p.Level + 1),
        b.UnitsIn(p).Select(u => ToDto(b, u, nowUtc)).ToList());

    private static UnitDto ToDto(Business b, BusinessAsset u, DateTime nowUtc)
    {
        var fault = b.FaultOf(u);
        return new UnitDto(
            u.Id,
            u.CatalogueId,
            u.Name,
            u.PurchasePrice,
            u.IncomePerSecond,
            b.UnitContributionPerSecond(u),
            u.StatusAt(nowUtc).ToString(),
            u.WorkingFrom,
            u.BreaksAt,
            fault.Id,
            fault.Name,
            b.RepairCostOf(u),
            b.FastRepairCostOf(u),
            fault.RepairHours);
    }

    public static CompanyDto ToDto(Company c, DateTime nowUtc) => new(
        c.Id,
        c.Name,
        c.Cash,
        c.PassiveIncomePerSecond,
        c.IncomePerSecondAt(nowUtc),
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
        GameConstants.BoostMultiplier,
        Services.StoreService.Avatar(c.AvatarId),
        c.TaxesDue,
        GameConstants.TaxRate,
        c.Businesses.Select(b => ToDto(b, nowUtc)).ToList());
}
