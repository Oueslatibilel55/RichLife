namespace RichLife.Domain.Store;

/// <summary>A profile badge sold for diamonds. <see cref="Name"/> is English; clients translate by id.</summary>
public sealed record BadgeDefinition(string Id, string Icon, string Name, int Price)
{
    /// <summary>Derived from the price, so the two can never disagree.</summary>
    public string Rarity => Price switch
    {
        >= 250 => "legendary",
        >= 120 => "epic",
        >= 50 => "rare",
        _ => "common",
    };
}

/// <summary>
/// The badge store, in display order. Rules, not content — like achievements and banks, they
/// live in code. Ids are stored once bought: never rename or reuse one.
/// </summary>
public static class BadgeCatalog
{
    public static readonly IReadOnlyList<BadgeDefinition> All =
    [
        new("rising-star",   "⭐", "Rising Star",       20),
        new("coffee-addict", "☕", "Coffee Addict",     20),
        new("early-bird",    "🐦", "Early Bird",        20),
        new("night-owl",     "🦉", "Night Owl",         20),
        new("lucky-clover",  "🍀", "Lucky",             50),
        new("on-fire",       "🔥", "On Fire",           50),
        new("ninja",         "🥷", "Ninja",             50),
        new("shark",         "🦈", "Shark",             50),
        new("rocket",        "🚀", "To the Moon",       120),
        new("unicorn",       "🦄", "Unicorn",           120),
        new("diamond-hands", "💎", "Diamond Hands",     120),
        new("alien",         "👽", "Out of This World", 120),
        new("lion",          "🦁", "King of the Jungle", 250),
        new("dragon",        "🐉", "Dragon",            250),
        new("champion",      "🏆", "Champion",          250),
        new("crown",         "👑", "Royalty",           250),
    ];

    public static BadgeDefinition? Find(string? id) => All.FirstOrDefault(b => b.Id == id);
}

/// <summary>Why diamonds moved — stored in the ledger (contract §6e).</summary>
public static class DiamondReasons
{
    public const string Welcome = "welcome";
    public const string Achievement = "achievement";
    public const string Prestige = "prestige";
    public const string Boost = "boost";
    public const string DoubleOffline = "double-offline";
    public const string Exchange = "exchange";
    public const string Badge = "badge";
    public const string Admin = "admin";
    public const string Backfill = "backfill";
}
