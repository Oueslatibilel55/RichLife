namespace RichLife.Domain;

public static class GameConstants
{
    /// <summary>Passive income every company starts with, before the prestige multiplier (features/016: was 3).</summary>
    public const decimal BasePassiveIncomePerSecond = 0.1m;

    /// <summary>Cash a new company starts with, so the first business opens at once (features/016).</summary>
    public const decimal StartingCash = 1_000m;

    // Pacing (features/016-pacing.md)

    /// <summary>Each further unit of the same type in a business costs this much more than the last.</summary>
    public const decimal UnitPriceGrowth = 1.2m;

    /// <summary>Each further place of a business costs this much more than the last.</summary>
    public const decimal PlacePriceGrowth = 1.4m;

    /// <summary>A business level costs at least this share of everything invested in the business.</summary>
    public const decimal BusinessLevelValueShare = 0.25m;

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

    // Diamonds and the store (features/011-diamonds-and-store.md)

    /// <summary>Every new company starts with this many diamonds.</summary>
    public const int StartingDiamonds = 25;

    /// <summary>Paid for each achievement unlocked.</summary>
    public const int AchievementDiamonds = 10;

    /// <summary>A prestige pays this many diamonds × the new level's number (P2 → 40, P7 → 140).</summary>
    public const int PrestigeDiamondsPerLevel = 20;

    /// <summary>Every income is multiplied by this while a boost runs.</summary>
    public const decimal BoostMultiplier = 2m;

    /// <summary>Boosts on sale: (hours, price in diamonds).</summary>
    public static readonly (int Hours, int Price)[] BoostOptions = [(1, 25), (3, 60), (8, 140)];

    /// <summary>Stacked boosts may not end further ahead than this.</summary>
    public static readonly TimeSpan MaxBoostAhead = TimeSpan.FromHours(24);

    /// <summary>Price of paying the last offline earnings a second time, and how long the offer lasts.</summary>
    public const int OfflineDoublePrice = 15;
    public static readonly TimeSpan OfflineDoubleWindow = TimeSpan.FromMinutes(30);

    /// <summary>Cash one diamond exchanges for — scales with prestige (1 % of the next prestige price).</summary>
    public static decimal DiamondCashValue(Enums.PrestigeLevel level) => level switch
    {
        Enums.PrestigeLevel.TheHustle     => 1_000m,
        Enums.PrestigeLevel.SmallBusiness => 5_000_000m,
        Enums.PrestigeLevel.Entrepreneur  => 22_000_000m,
        Enums.PrestigeLevel.BusinessMogul => 90_000_000m,
        Enums.PrestigeLevel.Tycoon        => 300_000_000m,
        Enums.PrestigeLevel.Billionaire   => 1_200_000_000m,
        _                                 => 1_200_000_000m,
    };

    // Business taxes (features/013-business-taxes.md)

    /// <summary>Share of what a business earned during a tax period that it owes as tax.</summary>
    public const decimal TaxRate = 0.07m;

    /// <summary>A business is billed once per period, counted from when it was opened.</summary>
    public static readonly TimeSpan TaxPeriod = TimeSpan.FromHours(24);

    // Bank loans (features/009-bank-loans.md)

    /// <summary>The bank collects one installment this often, counted from when the loan was taken.</summary>
    public static readonly TimeSpan LoanPaymentInterval = TimeSpan.FromHours(6);

    /// <summary>Offers are regenerated this often, on windows aligned to midnight UTC.</summary>
    public static readonly TimeSpan LoanOfferRotation = TimeSpan.FromHours(6);

    public const int LoanOffersPerLevel = 5;

    /// <summary>Added to the debt when an installment cannot be paid in full, as a share of the unpaid part.</summary>
    public const decimal LoanPenaltyRate = 0.10m;

    /// <summary>What a bank lends at each prestige level — like businesses, bigger loans unlock with prestige.</summary>
    public static (decimal Min, decimal Max) LoanAmountRange(Enums.PrestigeLevel level) => level switch
    {
        // About 5–40 % of the next prestige price (features/016).
        Enums.PrestigeLevel.TheHustle     => (5_000m, 40_000m),
        Enums.PrestigeLevel.SmallBusiness => (25_000_000m, 200_000_000m),
        Enums.PrestigeLevel.Entrepreneur  => (100_000_000m, 900_000_000m),
        Enums.PrestigeLevel.BusinessMogul => (400_000_000m, 3_500_000_000m),
        Enums.PrestigeLevel.Tycoon        => (1_500_000_000m, 12_000_000_000m),
        Enums.PrestigeLevel.Billionaire   => (6_000_000_000m, 48_000_000_000m),
        _                                 => (10_000_000_000m, 100_000_000_000m),
    };

    // Places and breakdowns (features/014-business-places-and-equipment.md)

    /// <summary>A business can run at most this many places.</summary>
    public const int MaxPlacesPerBusiness = 10;

    public const int MaxPlaceLevel = 10;

    /// <summary>Capacity of a place when the catalogue entry does not say.</summary>
    public const int DefaultPlaceCapacity = 4;
    public const int DefaultPlaceCapacityPerLevel = 2;

    /// <summary>Income bonus per place level above 1, on that place's income.</summary>
    public const decimal PlaceLevelIncomeBonus = 0.25m;

    /// <summary>Place level 1 → 2 costs this share of the place's price; each next level × <see cref="PlaceLevelCostGrowth"/> (Road to Rich).</summary>
    public const decimal PlaceLevelCostShare = 0.25m;
    public const decimal PlaceLevelCostGrowth = 1.35m;

    /// <summary>Income multiplier of a place by how busy it is.</summary>
    public static decimal PlaceTierMultiplier(Enums.PlaceTier tier) => tier switch
    {
        Enums.PlaceTier.Busy  => 1.5m,
        Enums.PlaceTier.Prime => 2.2m,
        _                     => 1m,
    };

    /// <summary>Mean time between failures of a unit when the catalogue does not say.</summary>
    public const int DefaultMtbfHours = 48;

    /// <summary>A unit's next failure comes after its MTBF × a uniform draw in [min, max).</summary>
    public const decimal BreakdownDrawMin = 0.5m;
    public const decimal BreakdownDrawMax = 1.5m;

    /// <summary>A fast repair costs this many times the standard price and is done at once.</summary>
    public const decimal FastRepairMultiplier = 2.5m;

    // Bourse (features/017-bourse.md)

    /// <summary>Prices step this often (every player sees the same tick).</summary>
    public const int StockTickSeconds = 30;

    /// <summary>Broker fee on every buy and every sell, as a share of the trade.</summary>
    public const decimal StockFeeRate = 0.01m;

    /// <summary>What is held on the Bourse (at cost) may not exceed this many hours of full income…</summary>
    public const int StockInvestHours = 24;

    /// <summary>…nor this floor, so a new company can still try it.</summary>
    public const decimal StockMinInvestLimit = 10_000m;

    // Marketplace closing fees
    public const decimal GracefulCloseFeeRate  = 0.10m;
    public const decimal EmergencyCloseFeeRate = 0.25m;
    public const decimal BankruptcyCloseFeeRate = 0.40m;
}
