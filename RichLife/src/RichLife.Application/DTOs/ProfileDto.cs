namespace RichLife.Application.DTOs;

// Player profile — contract §6b.

public record ProfileDto(
    string Username,
    string Email,
    string Country,
    DateTime MemberSince,
    int? Rank,
    int RankedPlayers,
    ProfileCompanyDto? Company,
    IReadOnlyList<OwnedLuxuryDto> Luxury,
    int AchievementsUnlocked,
    int AchievementsTotal,
    IReadOnlyList<AchievementDto> Achievements);

public record ProfileCompanyDto(
    string Name,
    DateTime CreatedAt,
    string PrestigeLevel,
    int PrestigeCount,
    decimal PrestigeMultiplier,
    decimal Cash,
    decimal NetWorth,
    decimal AllTimeEarnings,
    decimal IncomePerSecond,
    int Businesses,
    int Assets,
    int ManagersOnShift,
    int ManagersHired,
    int HighestBusinessLevel);

public record AchievementDto(
    string Code,
    string Title,
    string Description,
    string Icon,
    bool Unlocked,
    DateTime? UnlockedAt,
    decimal Current,
    decimal Target,
    string Unit);

/// <summary>Announced once, in the sync response that unlocked it.</summary>
public record AchievementUnlockedDto(string Code, string Title, string Icon);
