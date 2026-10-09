namespace RichLife.Application.DTOs;

// Store and diamonds — contract §6e.

public record StoreDto(
    int Diamonds,
    decimal Cash,
    DateTime? BoostUntil,
    decimal BoostMultiplier,
    int MaxBoostHours,
    IReadOnlyList<BoostOptionDto> Boosts,
    OfflineDoubleOfferDto? DoubleOffer,
    decimal DiamondValue,
    IReadOnlyList<StoreBadgeDto> Badges,
    string? FeaturedBadgeId,
    IReadOnlyList<DiamondTransactionDto> History);

public record BoostOptionDto(int Hours, int Price);

/// <summary>The last offline earnings, which can be paid again for <c>Price</c> diamonds until <c>Until</c>.</summary>
public record OfflineDoubleOfferDto(decimal Amount, int Price, DateTime Until);

public record StoreBadgeDto(string Id, string Icon, string Name, int Price, string Rarity, bool Owned, bool Featured);

public record OwnedBadgeDto(string Id, string Icon, string Name, string Rarity, DateTime PurchasedAt);

public record DiamondTransactionDto(int Amount, int Balance, string Reason, string? Detail, DateTime CreatedAt);

public record ExchangeDiamondsRequest(int Diamonds);

public record FeatureBadgeRequest(string? BadgeId);
