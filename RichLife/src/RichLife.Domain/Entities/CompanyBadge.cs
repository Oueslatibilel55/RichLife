namespace RichLife.Domain.Entities;

/// <summary>A badge the company bought, and when. Owned by <see cref="Company"/>; keyed by (company, badge).</summary>
public sealed class CompanyBadge
{
    public string BadgeId { get; private set; } = string.Empty;
    public DateTime PurchasedAt { get; private set; }

    private CompanyBadge() { }

    internal static CompanyBadge Create(string badgeId, DateTime purchasedAt) =>
        new() { BadgeId = badgeId, PurchasedAt = purchasedAt };
}
