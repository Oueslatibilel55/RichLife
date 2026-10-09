namespace RichLife.Domain.Entities;

/// <summary>An avatar the company bought, and when. Owned by <see cref="Company"/>; keyed by (company, avatar). Free avatars are never recorded.</summary>
public sealed class CompanyAvatar
{
    public string AvatarId { get; private set; } = string.Empty;
    public DateTime PurchasedAt { get; private set; }

    private CompanyAvatar() { }

    internal static CompanyAvatar Create(string avatarId, DateTime purchasedAt) =>
        new() { AvatarId = avatarId, PurchasedAt = purchasedAt };
}
