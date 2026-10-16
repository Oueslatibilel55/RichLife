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
    DateTime TaxPeriodEndsAt,
    int BrokenUnits,
    int MaxPlaces,
    IReadOnlyList<PlaceDto> Places
);

/// <summary>One place of a business (features/014-business-places-and-equipment.md).</summary>
public record PlaceDto(
    Guid Id,
    string KindId,
    string Name,
    string Street,
    string Tier,
    int Level,
    int MaxLevel,
    decimal Multiplier,
    int Capacity,
    int UnitCount,
    decimal BaseIncomePerSecond,
    decimal IncomePerSecond,
    decimal? NextLevelCost,
    int? NextLevelCapacity,
    IReadOnlyList<UnitDto> Units
);

/// <summary>
/// One unit in a place. It earns while <c>workingFrom &lt;= now &lt; breaksAt</c>; while it does,
/// it adds <c>contributionPerSecond</c> to the business's <c>netIncomePerSecond</c> (before prestige).
/// <c>fault</c> and the repair prices describe the failure due at <c>breaksAt</c>.
/// </summary>
public record UnitDto(
    Guid Id,
    string? CatalogueId,
    string Name,
    decimal Price,
    decimal IncomePerSecond,
    decimal ContributionPerSecond,
    string Status,
    DateTime WorkingFrom,
    DateTime BreaksAt,
    string Fault,
    string FaultName,
    decimal RepairCost,
    decimal FastRepairCost,
    int RepairHours
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
    int AutoRepairs,
    decimal RepairsPaid,
    CompanyDto Company
);

public record SyncResultDto(
    decimal AcceptedCash,
    bool Adjusted,
    IReadOnlyList<AchievementUnlockedDto> NewAchievements,
    LoanPaymentDto? LoanPayment,
    int Diamonds,
    decimal TaxesDue,
    decimal TaxBilled,
    int AutoRepairs,
    decimal RepairsPaid
);

/// <summary>Answer to paying taxes (contract §5): what was paid, and the company after it.</summary>
public record TaxPaymentDto(decimal Paid, decimal Cash, CompanyDto Company);
