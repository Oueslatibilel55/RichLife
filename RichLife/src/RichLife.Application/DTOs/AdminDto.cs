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
    int ManagerNames);

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
    DateTime? LastSeenAt);

public record SetAdminRoleRequest(bool IsAdmin);

public record SetCashRequest(decimal Cash);

public record ManagerNameDto(int Id, string Name, int InUse);

public record CreateManagerNameRequest(string Name);
