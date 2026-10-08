namespace RichLife.Domain;

public static class GameConstants
{
    /// <summary>Passive income every company starts with, before the prestige multiplier.</summary>
    public const decimal BasePassiveIncomePerSecond = 3m;

    /// <summary>Permanent income bonus granted per completed prestige.</summary>
    public const decimal PrestigeMultiplierPerLevel = 0.18m;

    // Passive income tiers: (cost, newRate)
    public static readonly (decimal Cost, decimal Rate)[] PassiveIncomeTiers =
    [
        (0m,           3m),
        (20_000m,      7m),
        (80_000m,      15m),
        (300_000m,     35m),
        (1_200_000m,   80m),
        (5_000_000m,   200m),
    ];

    /// <summary>Income per second a business asset yields, as a fraction of its price.</summary>
    public const decimal AssetIncomeRatio = 0.0003m;   // $100K truck -> $30/s

    /// <summary>Income per second a rental asset yields, as a fraction of its price.</summary>
    public const decimal RentalIncomeRatio = 0.0003m;  // $8K car -> $2.40/s

    /// <summary>
    /// Hiring a manager (automating a business so it earns offline) costs this many times
    /// the business's opening cost. One-time, not refunded on close.
    /// </summary>
    public const decimal ManagerCostMultiplier = 2m;

    /// <summary>A hired manager works this long, counted from the hire; then rehire.</summary>
    public static readonly TimeSpan ManagerShift = TimeSpan.FromHours(4);

    // Business levels
    public const int MaxBusinessLevel = 100;

    /// <summary>Income bonus per level above 1, on the business's whole gross income.</summary>
    public const decimal LevelIncomeBonus = 0.10m;

    /// <summary>Reaching each of these levels doubles the business's income.</summary>
    public static readonly int[] LevelMilestones = [10, 25, 50];

    /// <summary>Level 1 → 2 costs the opening cost; each next level costs this much more.</summary>
    public const decimal LevelCostGrowth = 1.25m;

    /// <summary>Seconds in the 30-day month used to amortize monthly salary costs.</summary>
    public const decimal SecondsPerSalaryMonth = 2_592_000m;

    // Offline cap
    public static readonly TimeSpan OfflineCap = TimeSpan.FromHours(4);

    /// <summary>
    /// Grace factor applied when validating a cash figure reported by the client,
    /// to absorb clock skew and rounding rather than punishing honest clients.
    /// </summary>
    public const decimal SyncTolerance = 1.05m;

    // Ad boost
    public const decimal AdIncomeMultiplier = 5m;
    public static readonly TimeSpan AdBoostDuration = TimeSpan.FromMinutes(30);
    public const int MaxAdBoostsPerDay = 10;

    // Marketplace closing fees
    public const decimal GracefulCloseFeeRate  = 0.10m;
    public const decimal EmergencyCloseFeeRate = 0.25m;
    public const decimal BankruptcyCloseFeeRate = 0.40m;
}
