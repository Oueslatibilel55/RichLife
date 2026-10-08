using RichLife.Domain.Enums;

namespace RichLife.Application.DTOs;

public record CompanyDto(
    Guid Id,
    string Name,
    decimal Cash,
    decimal PassiveIncomePerSecond,
    decimal IncomePerSecond,
    decimal OfflineIncomePerSecond,
    decimal NetWorth,
    decimal AllTimeEarnings,
    PrestigeLevel PrestigeLevel,
    int PrestigeCount,
    decimal PrestigeMultiplier,
    decimal NextPrestigeThreshold,
    DateTime LastSyncAt,
    IReadOnlyList<BusinessDto> Businesses
);

public record BusinessDto(
    Guid Id,
    string CatalogueId,
    string Name,
    string Sector,
    string RequiredPrestige,
    decimal OpeningCost,
    decimal NetIncomePerSecond,
    decimal TotalValue,
    bool IsAutomated,
    bool IsForSale,
    decimal? AskingPrice,
    int AssetCount,
    decimal ManagerCost,
    string? ManagerName,
    DateTime? ManagerUntil,
    int Level,
    decimal LevelMultiplier,
    decimal? NextLevelCost,
    decimal? NextLevelIncomePerSecond
);

public record OfflineEarningsDto(
    TimeSpan Elapsed,
    decimal Earned,
    decimal CashBefore,
    decimal CashAfter,
    bool Capped,
    CompanyDto Company
);

public record SyncResultDto(
    decimal AcceptedCash,
    bool Adjusted,
    IReadOnlyList<AchievementUnlockedDto> NewAchievements
);
