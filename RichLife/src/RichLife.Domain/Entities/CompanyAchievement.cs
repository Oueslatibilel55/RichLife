namespace RichLife.Domain.Entities;

/// <summary>
/// An achievement the company has unlocked, and when. Owned by <see cref="Company"/>; keyed by
/// (company, code). Kept even if the metric later drops — an unlock is permanent.
/// </summary>
public sealed class CompanyAchievement
{
    public string Code { get; private set; } = string.Empty;
    public DateTime UnlockedAt { get; private set; }

    private CompanyAchievement() { }

    internal static CompanyAchievement Create(string code, DateTime unlockedAt) =>
        new() { Code = code, UnlockedAt = unlockedAt };
}
