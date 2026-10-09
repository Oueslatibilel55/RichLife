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
    int Diamonds,
    DateTime? BoostUntil,
    decimal BoostMultiplier,
    AvatarDto? Avatar,
    decimal TaxesDue,
    decimal TaxRate,
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
    decimal? NextLevelIncomePerSecond,
    decimal TaxDue,
    decimal TaxAccruing,
    DateTime TaxPeriodEndsAt
);

public record OfflineEarningsDto(
    TimeSpan Elapsed,
    decimal Earned,
    decimal CashBefore,
    decimal CashAfter,
    bool Capped,
    LoanPaymentDto? LoanPayment,
    OfflineDoubleOfferDto? DoubleOffer,
    decimal TaxBilled,
    CompanyDto Company
);

public record SyncResultDto(
    decimal AcceptedCash,
    bool Adjusted,
    IReadOnlyList<AchievementUnlockedDto> NewAchievements,
    LoanPaymentDto? LoanPayment,
    int Diamonds,
    decimal TaxesDue,
    decimal TaxBilled
);

/// <summary>Answer to paying taxes (contract §5): what was paid, and the company after it.</summary>
public record TaxPaymentDto(decimal Paid, decimal Cash, CompanyDto Company);
