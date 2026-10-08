using System.Globalization;
using RichLife.Domain.Achievements;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Common;
using RichLife.Domain.Enums;
using RichLife.Domain.Events;

namespace RichLife.Domain.Entities;

public class Company : AggregateRoot
{
    public Guid PlayerId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    // Economy
    public decimal Cash { get; private set; }

    /// <summary>
    /// Set when an admin overwrites cash (or resets the company). The next <see cref="Sync"/>
    /// then keeps the server figure instead of the client's — otherwise an online player's
    /// next sync would report their old, lower cash and silently undo the change.
    /// </summary>
    public bool CashOverridePending { get; private set; }
    public decimal PassiveIncomePerSecond { get; private set; } = GameConstants.BasePassiveIncomePerSecond;
    public decimal AllTimeEarnings { get; private set; }

    /// <summary>Cash plus the liquidation value of everything owned.</summary>
    public decimal NetWorth =>
        Cash
        + _assets.Sum(a => a.CurrentValue)
        + _businesses.Sum(b => b.TotalValue)
        + _luxuryAssets.Sum(l => l.Cost);

    // Prestige
    public PrestigeLevel PrestigeLevel { get; private set; } = PrestigeLevel.TheHustle;
    public int PrestigeCount { get; private set; }
    public decimal PrestigeMultiplier => 1m + (GameConstants.PrestigeMultiplierPerLevel * PrestigeCount);

    /// <summary>Income while the player is connected: every business contributes.</summary>
    public decimal IncomePerSecond =>
        (PassiveIncomePerSecond + _businesses.Sum(b => b.NetIncomePerSecond)) * PrestigeMultiplier;

    /// <summary>
    /// Income per second while away, as of <paramref name="atUtc"/>: base income plus every
    /// business whose manager shift is running then. What is actually paid for a period
    /// away is computed per shift by <see cref="ApplyOfflineProgress"/>.
    /// </summary>
    public decimal OfflineIncomePerSecondAt(DateTime atUtc) =>
        (PassiveIncomePerSecond + _businesses.Where(b => b.HasManagerAt(atUtc)).Sum(b => b.NetIncomePerSecond))
        * PrestigeMultiplier;

    // Sync
    public DateTime LastSyncAt { get; private set; } = DateTime.UtcNow;

    // Collections
    private readonly List<Business> _businesses = [];
    private readonly List<Asset> _assets = [];
    private readonly List<LuxuryAsset> _luxuryAssets = [];
    private readonly List<CompanyAchievement> _achievements = [];

    public IReadOnlyList<Business> Businesses => _businesses.AsReadOnly();
    public IReadOnlyList<Asset> Assets => _assets.AsReadOnly();
    public IReadOnlyList<LuxuryAsset> LuxuryAssets => _luxuryAssets.AsReadOnly();
    public IReadOnlyList<CompanyAchievement> Achievements => _achievements.AsReadOnly();

    private Company() { }

    public static Company Create(Guid playerId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Company { PlayerId = playerId, Name = name };
    }

    // -- Economy ------------------------------------------------------------

    /// <summary>
    /// Credits a period spent away, starting at <see cref="LastSyncAt"/>. The paid window is
    /// capped at <see cref="GameConstants.OfflineCap"/>; base income is paid for all of it,
    /// each business only for the part its manager's shift covers. Returns the amount credited.
    /// </summary>
    public decimal ApplyOfflineProgress(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero) return 0m;

        var effective = elapsed > GameConstants.OfflineCap ? GameConstants.OfflineCap : elapsed;
        var from = LastSyncAt;
        var to = from + effective;

        var baseEarned = PassiveIncomePerSecond * (decimal)effective.TotalSeconds;
        var managed = _businesses.Sum(b => b.NetIncomePerSecond * b.ManagedSecondsWithin(from, to));
        var earned = (baseEarned + managed) * PrestigeMultiplier;

        AddCash(earned);
        return earned;
    }

    /// <summary>
    /// Credits offline earnings accumulated since the last sync and moves the sync
    /// watermark forward. This is the only place that advances <see cref="LastSyncAt"/>
    /// for accrual, so <c>/state</c> and <c>/sync</c> cannot double-credit the same window.
    /// </summary>
    public decimal AccrueOffline(DateTime nowUtc)
    {
        var earned = ApplyOfflineProgress(nowUtc - LastSyncAt);
        LastSyncAt = nowUtc;
        MarkUpdated();
        return earned;
    }

    /// <summary>
    /// Highest cash figure the client could legitimately hold right now: what the
    /// server last recorded, plus full online income for the elapsed window, plus a
    /// small tolerance for clock skew.
    /// </summary>
    public decimal MaxPlausibleCash(DateTime nowUtc)
    {
        var elapsed = nowUtc - LastSyncAt;
        var seconds = elapsed <= TimeSpan.Zero ? 0m : (decimal)elapsed.TotalSeconds;
        return Cash + (IncomePerSecond * seconds * GameConstants.SyncTolerance);
    }

    /// <summary>
    /// Accepts the client's cash figure, clamped to what could plausibly have been
    /// earned since the last sync. The client simulates income locally, so its number
    /// is useful — but it is never trusted beyond this ceiling.
    /// </summary>
    public Result Sync(decimal clientCash, DateTime nowUtc)
    {
        if (clientCash < 0m) return Result.Fail("Cash cannot be negative.");

        if (CashOverridePending)
        {
            // An admin set this figure since the client last synced: it wins, once.
            CashOverridePending = false;
            LastSyncAt = nowUtc;
            MarkUpdated();
            return Result.Ok();
        }

        var ceiling = MaxPlausibleCash(nowUtc);
        var accepted = clientCash > ceiling ? ceiling : clientCash;

        var gained = accepted - Cash;
        if (gained > 0m) AllTimeEarnings += gained;

        Cash = accepted;
        LastSyncAt = nowUtc;
        MarkUpdated();
        return Result.Ok();
    }

    public void AddCash(decimal amount)
    {
        if (amount <= 0) return;
        Cash += amount;
        AllTimeEarnings += amount;
        MarkUpdated();
    }

    public Result DeductCash(decimal amount)
    {
        if (amount > Cash) return Result.Fail("Insufficient funds.");
        Cash -= amount;
        MarkUpdated();
        return Result.Ok();
    }

    // -- Business -----------------------------------------------------------

    /// <summary>
    /// Opens a business from a catalogue entry, stamped with the entry's numbers as they
    /// stand right now. A retired entry is reported exactly like an unknown one.
    /// </summary>
    public Result<Business> OpenBusiness(BusinessCatalogueEntry entry)
    {
        if (!entry.IsActive)
            return Result.Fail<Business>("Business not found in catalogue.");
        if (entry.RequiredPrestige > PrestigeLevel)
            return Result.Fail<Business>($"Requires prestige {entry.RequiredPrestige}.");
        if (_businesses.Any(b => b.CatalogueId == entry.Id))
            return Result.Fail<Business>("You already own this business.");

        var deduct = DeductCash(entry.OpeningCost);
        if (!deduct.IsSuccess) return Result.Fail<Business>(deduct.Error!);

        var business = entry.CreateBusiness(Id);
        _businesses.Add(business);
        RaiseDomainEvent(new BusinessOpenedEvent(Id, business.Id, business.Name));
        MarkUpdated();
        return Result.Ok(business);
    }

    /// <summary>
    /// Buys one of <paramref name="entry"/>'s assets for an owned business. Deliberately
    /// allowed on a retired entry: retiring stops new openings, not existing owners.
    /// </summary>
    public Result<Business> BuyAsset(Guid businessId, BusinessCatalogueEntry entry, string assetCatalogueId)
    {
        var business = _businesses.FirstOrDefault(b => b.Id == businessId);
        if (business is null) return Result.Fail<Business>("Business not found.");

        if (business.CatalogueId != entry.Id)
            throw new ArgumentException(
                $"Catalogue entry '{entry.Id}' does not match business '{business.CatalogueId}'.",
                nameof(entry));

        var asset = entry.FindAsset(assetCatalogueId);
        if (asset is null) return Result.Fail<Business>("Asset not available for this business.");

        if (business.Assets.Count < asset.UnlockAtAssetCount)
            return Result.Fail<Business>($"Need {asset.UnlockAtAssetCount} assets to unlock this.");

        var deduct = DeductCash(asset.Price);
        if (!deduct.IsSuccess) return Result.Fail<Business>(deduct.Error!);

        business.AddAsset(BusinessAsset.Create(business.Id, asset.Name, asset.Price, entry.IncomeFor(asset)));
        MarkUpdated();
        return Result.Ok(business);
    }

    /// <summary>
    /// Hires <paramref name="manager"/> for a 4-hour shift (<see cref="GameConstants.ManagerShift"/>), from
    /// <paramref name="nowUtc"/>: the business earns offline while it runs. Paid per shift. The
    /// caller picks the name (randomness and the name table are infrastructure concerns).
    /// </summary>
    public Result<Business> AutomateBusiness(Guid businessId, ManagerName manager, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var business = _businesses.FirstOrDefault(b => b.Id == businessId);
        if (business is null) return Result.Fail<Business>("Business not found.");
        if (business.HasManagerAt(nowUtc)) return Result.Fail<Business>("Business already has a manager.");

        var deduct = DeductCash(business.ManagerCost);
        if (!deduct.IsSuccess) return Result.Fail<Business>(deduct.Error!);

        business.HireManager(manager, nowUtc);
        MarkUpdated();
        return Result.Ok(business);
    }

    /// <summary>
    /// Pays <see cref="Business.NextLevelCost"/> and raises the business one level, boosting
    /// its income. Paid from cash; not part of the business's liquidation value.
    /// </summary>
    public Result<Business> LevelUpBusiness(Guid businessId)
    {
        var business = _businesses.FirstOrDefault(b => b.Id == businessId);
        if (business is null) return Result.Fail<Business>("Business not found.");
        if (business.NextLevelCost is not { } cost)
            return Result.Fail<Business>("Business is already at max level.");

        var deduct = DeductCash(cost);
        if (!deduct.IsSuccess) return Result.Fail<Business>(deduct.Error!);

        business.LevelUp();
        MarkUpdated();
        return Result.Ok(business);
    }

    // -- Luxury -----------------------------------------------------------------

    /// <summary>
    /// Buys a luxury item: status only (no income), one of each, paid from cash. It counts
    /// toward <see cref="NetWorth"/> at its price.
    /// </summary>
    public Result<LuxuryAsset> BuyLuxury(LuxuryCatalogueEntry item, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.IsActive) return Result.Fail<LuxuryAsset>("Item not found.");
        if (PrestigeLevel < item.RequiredPrestige)
            return Result.Fail<LuxuryAsset>($"Requires prestige {item.RequiredPrestige}.");
        if (_luxuryAssets.Any(l => l.CatalogueId == item.Id))
            return Result.Fail<LuxuryAsset>("You already own this.");

        var deduct = DeductCash(item.Price);
        if (!deduct.IsSuccess) return Result.Fail<LuxuryAsset>(deduct.Error!);

        var owned = LuxuryAsset.Create(Id, item, nowUtc);
        _luxuryAssets.Add(owned);
        MarkUpdated();
        return Result.Ok(owned);
    }

    // -- Achievements -----------------------------------------------------------

    /// <summary>The company's current value for an achievement metric.</summary>
    public decimal AchievementMetricValue(AchievementMetric metric) => metric switch
    {
        AchievementMetric.AllTimeEarnings      => AllTimeEarnings,
        AchievementMetric.CashOnHand           => Cash,
        AchievementMetric.BusinessesOwned      => _businesses.Count,
        AchievementMetric.AssetsOwned          => _businesses.Sum(b => b.Assets.Count),
        AchievementMetric.ManagersHired        => _businesses.Count(b => b.ManagerNameId is not null),
        AchievementMetric.HighestBusinessLevel => _businesses.Count == 0 ? 0 : _businesses.Max(b => b.Level),
        AchievementMetric.PrestigeCount        => PrestigeCount,
        AchievementMetric.LuxuryOwned          => _luxuryAssets.Count,
        _ => 0m,
    };

    /// <summary>
    /// Records every achievement whose target is now met and that was not unlocked before.
    /// Returns the newly unlocked ones (usually none). An unlock is permanent.
    /// </summary>
    public IReadOnlyList<AchievementDefinition> UnlockAchievements(DateTime nowUtc)
    {
        var unlocked = _achievements.Select(a => a.Code).ToHashSet();
        var fresh = AchievementCatalog.All
            .Where(a => !unlocked.Contains(a.Code) && AchievementMetricValue(a.Metric) >= a.Target)
            .ToList();

        foreach (var a in fresh) _achievements.Add(CompanyAchievement.Create(a.Code, nowUtc));
        if (fresh.Count > 0) MarkUpdated();
        return fresh;
    }

    // -- Admin ----------------------------------------------------------------

    /// <summary>Admin: sets cash outright. Not earnings — <see cref="AllTimeEarnings"/> is untouched.</summary>
    public Result AdminSetCash(decimal cash)
    {
        if (cash < 0m) return Result.Fail("Cash cannot be negative.");
        Cash = cash;
        CashOverridePending = true;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>
    /// Admin: a fresh start — no cash, no businesses, first prestige level, base income,
    /// earnings (and so leaderboard rank) back to zero. The company itself and its name stay.
    /// </summary>
    public void AdminReset(DateTime nowUtc)
    {
        _businesses.Clear();
        _achievements.Clear();
        _luxuryAssets.Clear();
        Cash = 0m;
        AllTimeEarnings = 0m;
        PrestigeLevel = PrestigeLevel.TheHustle;
        PrestigeCount = 0;
        PassiveIncomePerSecond = GameConstants.BasePassiveIncomePerSecond;
        LastSyncAt = nowUtc;
        CashOverridePending = true;
        MarkUpdated();
    }

    public Result ListBusinessForSale(Guid businessId, decimal askingPrice)
    {
        var biz = _businesses.FirstOrDefault(b => b.Id == businessId);
        if (biz is null) return Result.Fail("Business not found.");
        biz.ListForSale(askingPrice);
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>
    /// Closes a business and refunds its liquidation value — the opening cost plus
    /// every asset bought for it — minus the closing fee.
    /// </summary>
    public Result CloseBusiness(Guid businessId, bool emergency = false)
    {
        var biz = _businesses.FirstOrDefault(b => b.Id == businessId);
        if (biz is null) return Result.Fail("Business not found.");

        var feeRate = emergency
            ? GameConstants.EmergencyCloseFeeRate
            : GameConstants.GracefulCloseFeeRate;

        // A refund returns capital; it is not income, so it does not count toward
        // AllTimeEarnings.
        Cash += biz.TotalValue * (1m - feeRate);
        _businesses.Remove(biz);
        MarkUpdated();
        return Result.Ok();
    }

    // -- Upgrade passive income ---------------------------------------------

    public Result UpgradePassiveIncome(decimal cost, decimal newRate)
    {
        if (newRate <= PassiveIncomePerSecond)
            return Result.Fail("That upgrade is not an improvement.");

        var deduct = DeductCash(cost);
        if (!deduct.IsSuccess) return deduct;

        PassiveIncomePerSecond = newRate;
        MarkUpdated();
        return Result.Ok();
    }

    // -- Prestige -----------------------------------------------------------

    public Result CanPrestige()
    {
        if ((int)PrestigeLevel >= 7)
            return Result.Fail("Already at maximum prestige.");
        // Prestige is paid for, so only cash counts — business value cannot be spent.
        if (Cash < GetPrestigeThreshold())
            // Invariant culture: `:C0` printed "25 000 €" on a French-locale server.
            return Result.Fail(string.Create(
                CultureInfo.InvariantCulture, $"Prestige costs ${GetPrestigeThreshold():N0} in cash."));
        return Result.Ok();
    }

    /// <summary>
    /// Buys the next prestige tier and its permanent income multiplier. The price comes out
    /// of cash; the leftover cash and every business are kept.
    /// </summary>
    public Result Prestige(DateTime nowUtc)
    {
        var canPrestige = CanPrestige();
        if (!canPrestige.IsSuccess) return canPrestige;

        var paid = DeductCash(GetPrestigeThreshold());
        if (!paid.IsSuccess) return paid;

        PrestigeCount++;
        PrestigeLevel = (PrestigeLevel)((int)PrestigeLevel + 1);

        PassiveIncomePerSecond = GameConstants.BasePassiveIncomePerSecond * PrestigeMultiplier;
        LastSyncAt = nowUtc;

        RaiseDomainEvent(new PrestigeTriggeredEvent(Id, PrestigeLevel, PrestigeMultiplier));
        MarkUpdated();
        return Result.Ok();
    }

    public decimal GetPrestigeThreshold() => PrestigeLevel switch
    {
        PrestigeLevel.TheHustle => 25_000m,
        PrestigeLevel.SmallBusiness => 200_000m,
        PrestigeLevel.Entrepreneur => 2_000_000m,
        PrestigeLevel.BusinessMogul => 20_000_000m,
        PrestigeLevel.Tycoon => 250_000_000m,
        PrestigeLevel.Billionaire => 3_000_000_000m,
        _ => decimal.MaxValue
    };
}
