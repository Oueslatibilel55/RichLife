namespace RichLife.Application.DTOs;

// Admin panel — contract §7b.

public record AdminStatsDto(
    DateTime GeneratedAt,
    int Players,
    int Admins,
    int NewPlayers24h,
    int NewPlayers7d,
    int ActivePlayers24h,
    int Companies,
    decimal TotalCash,
    decimal TotalAllTimeEarnings,
    int BusinessesOwned,
    int ManagersOnShift,
    decimal AverageBusinessLevel,
    IReadOnlyList<PrestigeCountDto> PrestigeDistribution,
    IReadOnlyList<TopBusinessDto> TopBusinesses,
    int CatalogueBusinesses,
    int CatalogueActive,
    int CatalogueAssets,
    int ManagerNames,
    int LuxuryOwned,
    int LuxuryCatalogue,
    int LuxuryCatalogueActive,
    int AchievementsUnlocked,
    IReadOnlyList<AchievementCountDto> AchievementDistribution,
    int LoansTaken,
    int ActiveLoans,
    decimal LoansOutstanding,
    int LoansMissedPayments,
    int DiamondsInCirculation,
    int DiamondsEarned,
    int DiamondsSpent,
    int BadgesOwned,
    int BoostsActive,
    IReadOnlyList<BadgeCountDto> BadgeDistribution,
    int AvatarsOwned,
    IReadOnlyList<AvatarCountDto> AvatarDistribution);

public record AvatarCountDto(string Id, string Icon, string Name, int Price, int Owners, int InUse);

public record BadgeCountDto(string Id, string Icon, string Name, int Price, int Owners);

public record AchievementCountDto(string Code, string Title, string Icon, int Companies);

public record PrestigeCountDto(string Level, int Companies);

public record TopBusinessDto(string CatalogueId, string Name, int Owners);

public record AdminPlayerDto(
    Guid Id,
    string Username,
    string Email,
    string Country,
    bool IsAdmin,
    DateTime CreatedAt,
    string? CompanyName,
    decimal? Cash,
    string? PrestigeLevel,
    int? PrestigeCount,
    decimal? AllTimeEarnings,
    int? Businesses,
    DateTime? LastSeenAt,
    int? HighestBusinessLevel,
    int? LuxuryOwned,
    int? AchievementsUnlocked,
    decimal? LoanOutstanding,
    int? Diamonds,
    int? Badges);

public record SetAdminRoleRequest(bool IsAdmin);

public record SetCashRequest(decimal Cash);

public record AdjustDiamondsRequest(int Amount, string? Reason);

public record ManagerNameDto(int Id, string Name, int InUse);

public record CreateManagerNameRequest(string Name);

// Bank — contract §7c.

public record AdminLoanDto(
    Guid Id,
    Guid PlayerId,
    string Username,
    string CompanyName,
    string BankId,
    string BankName,
    string BankIcon,
    decimal Principal,
    decimal InterestRate,
    decimal TotalRepay,
    decimal Paid,
    decimal Penalties,
    decimal Outstanding,
    int MissedPayments,
    DateTime TakenAt,
    DateTime? NextPaymentAt,
    DateTime? RepaidAt,
    bool Forgiven);

public record AdminBankDto(
    string Id,
    string Name,
    string Icon,
    decimal MinRate,
    decimal MaxRate,
    int MinInstallments,
    int MaxInstallments,
    int LoansTaken,
    int ActiveLoans,
    decimal TotalLent);

/// <summary>Per-bank usage, read from the loans table.</summary>
public record BankUsage(string BankId, int LoansTaken, int ActiveLoans, decimal TotalLent);

// Luxury catalogue editor — contract §7d.

public record AdminLuxuryItemDto(
    string Id,
    string Name,
    string Category,
    string Description,
    decimal Price,
    string RequiredPrestige,
    string ImageUrl,
    string ImageCredit,
    string ImageSourceUrl,
    int DisplayOrder,
    bool IsActive,
    int Owners);

public record CreateLuxuryItemRequest(
    string Id,
    string Name,
    Domain.Enums.LuxuryCategory Category,
    string? Description,
    decimal Price,
    Domain.Enums.PrestigeLevel RequiredPrestige,
    string ImageUrl,
    string? ImageCredit,
    string? ImageSourceUrl,
    int DisplayOrder);

public record UpdateLuxuryItemRequest(
    string Name,
    Domain.Enums.LuxuryCategory Category,
    string? Description,
    decimal Price,
    Domain.Enums.PrestigeLevel RequiredPrestige,
    string ImageUrl,
    string? ImageCredit,
    string? ImageSourceUrl,
    int DisplayOrder,
    bool IsActive);
