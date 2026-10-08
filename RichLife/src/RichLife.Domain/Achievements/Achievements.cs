namespace RichLife.Domain.Achievements;

/// <summary>What an achievement measures. Read off the company by <c>Company.AchievementMetric</c>.</summary>
public enum AchievementMetric
{
    AllTimeEarnings,
    CashOnHand,
    BusinessesOwned,
    AssetsOwned,
    ManagersHired,
    HighestBusinessLevel,
    PrestigeCount,
    LuxuryOwned,
}

/// <summary>One achievement: reached once <see cref="Metric"/> is at least <see cref="Target"/>.</summary>
public sealed record AchievementDefinition(
    string Code,
    string Icon,
    string Title,
    string Description,
    AchievementMetric Metric,
    decimal Target);

/// <summary>
/// The achievement list, in display order. Rules, not content — they live in code like
/// <see cref="GameConstants"/>. Codes are stored once unlocked: never rename or reuse one.
/// </summary>
public static class AchievementCatalog
{
    public static readonly IReadOnlyList<AchievementDefinition> All =
    [
        new("earn-1k",        "💵", "First thousand",    "Earn $1,000 all-time.",               AchievementMetric.AllTimeEarnings,      1_000m),
        new("first-business", "🏪", "Open for business", "Own your first business.",            AchievementMetric.BusinessesOwned,      1m),
        new("assets-10",      "📦", "Stocked up",        "Own 10 assets across your businesses.", AchievementMetric.AssetsOwned,        10m),
        new("first-manager",  "🧑‍💼", "Delegator",         "Hire your first manager.",            AchievementMetric.ManagersHired,        1m),
        new("earn-100k",      "💰", "Six figures",       "Earn $100,000 all-time.",             AchievementMetric.AllTimeEarnings,      100_000m),
        new("businesses-5",   "🏬", "Small chain",       "Own 5 businesses at once.",           AchievementMetric.BusinessesOwned,      5m),
        new("level-10",       "⭐", "Double down",       "Get a business to level 10.",         AchievementMetric.HighestBusinessLevel, 10m),
        new("prestige-1",     "🏆", "Moving up",         "Prestige for the first time.",        AchievementMetric.PrestigeCount,        1m),
        new("first-luxury",   "🛥️", "Living large",      "Buy your first luxury item.",         AchievementMetric.LuxuryOwned,          1m),
        new("managers-5",     "👔", "Head of HR",        "Hire managers for 5 businesses.",     AchievementMetric.ManagersHired,        5m),
        new("earn-1m",        "🤑", "Millionaire",       "Earn $1,000,000 all-time.",           AchievementMetric.AllTimeEarnings,      1_000_000m),
        new("cash-1m",        "💎", "Liquid",            "Hold $1,000,000 in cash.",            AchievementMetric.CashOnHand,           1_000_000m),
        new("assets-50",      "🏗️", "Asset machine",     "Own 50 assets across your businesses.", AchievementMetric.AssetsOwned,        50m),
        new("businesses-10",  "🏙️", "Conglomerate",      "Own 10 businesses at once.",          AchievementMetric.BusinessesOwned,      10m),
        new("level-25",       "🌟", "Quadruple",         "Get a business to level 25.",         AchievementMetric.HighestBusinessLevel, 25m),
        new("prestige-3",     "🥇", "Business mogul",    "Reach P4 — Business Mogul.",          AchievementMetric.PrestigeCount,        3m),
        new("earn-1b",        "🏦", "Billionaire",       "Earn $1,000,000,000 all-time.",       AchievementMetric.AllTimeEarnings,      1_000_000_000m),
        new("prestige-6",     "👑", "Global empire",     "Reach the top prestige level.",       AchievementMetric.PrestigeCount,        6m),
    ];

    public static AchievementDefinition? Find(string code) => All.FirstOrDefault(a => a.Code == code);
}
