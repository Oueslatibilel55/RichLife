using RichLife.Domain.Enums;

namespace RichLife.Application.DTOs;

public record LeaderboardEntryDto(
    int Rank,
    string Username,
    string Country,
    string CompanyName,
    decimal AllTimeEarnings,
    PrestigeLevel PrestigeLevel,
    int PrestigeCount,
    string? BadgeIcon,
    AvatarDto? Avatar
);
