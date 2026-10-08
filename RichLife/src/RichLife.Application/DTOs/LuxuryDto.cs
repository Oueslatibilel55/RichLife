namespace RichLife.Application.DTOs;

// Luxury collection — contract §6c.

public record LuxuryItemDto(
    string Id,
    string Name,
    string Category,
    string Description,
    decimal Price,
    string RequiredPrestige,
    string ImageUrl,
    string ImageCredit,
    string ImageSourceUrl,
    bool IsUnlocked,
    bool IsOwned,
    bool CanAfford);

public record OwnedLuxuryDto(
    string Id,
    string Name,
    string Category,
    decimal Price,
    string ImageUrl,
    string ImageCredit,
    DateTime PurchasedAt);
