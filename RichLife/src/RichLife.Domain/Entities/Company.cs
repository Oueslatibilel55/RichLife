using System.Globalization;
using RichLife.Domain.Achievements;
using RichLife.Domain.Banking;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Common;
using RichLife.Domain.Enums;
using RichLife.Domain.Events;
using RichLife.Domain.Store;

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

    // Diamonds — contract §6e. The balance; every change also writes a ledger line.
    public int Diamonds { get; private set; }

    /// <summary>Every income is × <see cref="GameConstants.BoostMultiplier"/> until then (online and offline).</summary>
    public DateTime? BoostUntil { get; private set; }

    /// <summary>The last offline earnings, which can be paid again for diamonds until <see cref="OfflineBonusUntil"/>.</summary>
    public decimal OfflineBonusAmount { get; private set; }
    public DateTime? OfflineBonusUntil { get; private set; }

    /// <summary>The bought badge shown next to the name; null for none.</summary>
    public string? FeaturedBadgeId { get; private set; }

    /// <summary>The profile avatar in use (a free or bought one); null shows the initial.</summary>
    public string? AvatarId { get; private set; }

    /// <summary>Every tax ever paid (features/013-business-taxes.md).</summary>
    public decimal TaxesPaid { get; private set; }

    /// <summary>Tax billed to the businesses and not yet paid. Prestige waits until it is zero.</summary>
    public decimal TaxesDue => _businesses.Sum(b => b.TaxDue);

    /// <summary>Cash plus the liquidation value of everything owned, minus what is still owed to a bank and in taxes.</summary>
    public decimal NetWorth =>
        Cash
        + _assets.Sum(a => a.CurrentValue)
        + _businesses.Sum(b => b.TotalValue)
        + _luxuryAssets.Sum(l => l.Cost)
        - (ActiveLoan?.Outstanding ?? 0m)
        - TaxesDue;

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
    private readonly List<Loan> _loans = [];
    private readonly List<CompanyBadge> _badges = [];
    private readonly List<CompanyAvatar> _avatars = [];
    private readonly List<DiamondTransaction> _diamondLedger = [];

    public IReadOnlyList<Business> Businesses => _businesses.AsReadOnly();
    public IReadOnlyList<Asset> Assets => _assets.AsReadOnly();
    public IReadOnlyList<LuxuryAsset> LuxuryAssets => _luxuryAssets.AsReadOnly();
    public IReadOnlyList<CompanyAchievement> Achievements => _achievements.AsReadOnly();
    public IReadOnlyList<Loan> Loans => _loans.AsReadOnly();
    public IReadOnlyList<CompanyBadge> Badges => _badges.AsReadOnly();

    /// <summary>Avatars bought — free ones are available to everyone and never listed here.</summary>
    public IReadOnlyList<CompanyAvatar> Avatars => _avatars.AsReadOnly();

    /// <summary>
    /// Ledger lines added since the company was loaded — the ledger itself is never loaded
    /// (it only grows). Read history through the repository.
    /// </summary>
    public IReadOnlyList<DiamondTransaction> DiamondLedger => _diamondLedger.AsReadOnly();

    /// <summary>The loan being repaid, if any — there is never more than one.</summary>
    public Loan? ActiveLoan => _loans.FirstOrDefault(l => l.IsActive);

    private Company() { }

    public static Company Create(Guid playerId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var company = new Company { PlayerId = playerId, Name = name };
        company.GrantDiamonds(GameConstants.StartingDiamonds, DiamondReasons.Welcome, null, company.CreatedAt);
        return company;
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

        // A boost pays the boosted part of the window (BoostMultiplier − 1) more times.
        var extra = GameConstants.BoostMultiplier - 1m;
        var boostEnd = BoostUntil is { } until && until > from ? (until < to ? until : to) : (DateTime?)null;

        var baseEarned = PassiveIncomePerSecond
            * ((decimal)effective.TotalSeconds + extra * BoostedSecondsWithin(from, to));
        var earned = baseEarned * PrestigeMultiplier;
        foreach (var b in _businesses)
        {
            var seconds = b.ManagedSecondsWithin(from, to)
                + (boostEnd is { } end ? extra * b.ManagedSecondsWithin(from, end) : 0m);
            var businessEarned = b.NetIncomePerSecond * seconds * PrestigeMultiplier;
            b.RecordEarnings(businessEarned);   // what each business made is what it is taxed on
            earned += businessEarned;
        }

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
        seconds += (GameConstants.BoostMultiplier - 1m) * BoostedSecondsWithin(LastSyncAt, nowUtc);
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
        if (gained > 0m)
        {
            AllTimeEarnings += gained;
            RecordOnlineBusinessEarnings(gained);
        }

        Cash = accepted;
        LastSyncAt = nowUtc;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>
    /// Online every business earns, and the client reports only the total: each business is
    /// credited its share of it by income rate (the prestige and boost multipliers apply to
    /// all alike). A business losing money (salary above income) earns nothing taxable.
    /// </summary>
    private void RecordOnlineBusinessEarnings(decimal gained)
    {
        var total = PassiveIncomePerSecond + _businesses.Sum(b => Math.Max(b.NetIncomePerSecond, 0m));
        if (total <= 0m) return;
        foreach (var b in _businesses)
            if (b.NetIncomePerSecond > 0m) b.RecordEarnings(gained * b.NetIncomePerSecond / total);
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

    // -- Taxes (features/013-business-taxes.md) -------------------------------------

    /// <summary>
    /// Bills every business whose tax period has ended: <see cref="GameConstants.TaxRate"/> of what
    /// it earned. Nothing is taken from cash — the player pays by hand, so cash never goes
    /// negative. Called by <c>/sync</c> and <c>/state</c>. Returns the total newly billed.
    /// </summary>
    public decimal AssessTaxes(DateTime nowUtc)
    {
        var billed = _businesses.Sum(b => b.AssessTaxes(nowUtc));
        if (billed > 0m) MarkUpdated();
        return billed;
    }

    /// <summary>Pays one business's whole bill from cash. Paying is spending, not a loss of earnings.</summary>
    public Result<decimal> PayBusinessTaxes(Guid businessId)
    {
        var business = _businesses.FirstOrDefault(b => b.Id == businessId);
        if (business is null) return Result.Fail<decimal>("Business not found.");
        if (business.TaxDue <= 0m) return Result.Fail<decimal>("No taxes due.");

        var deduct = DeductCash(business.TaxDue);
        if (!deduct.IsSuccess) return Result.Fail<decimal>(deduct.Error!);

        var paid = business.PayTaxes();
        TaxesPaid += paid;
        MarkUpdated();
        return Result.Ok(paid);
    }

    /// <summary>Pays every business's bill at once — all or nothing.</summary>
    public Result<decimal> PayAllTaxes()
    {
        var due = TaxesDue;
        if (due <= 0m) return Result.Fail<decimal>("No taxes due.");

        var deduct = DeductCash(due);
        if (!deduct.IsSuccess) return Result.Fail<decimal>(deduct.Error!);

        foreach (var b in _businesses) TaxesPaid += b.PayTaxes();
        MarkUpdated();
        return Result.Ok(due);
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

    // -- Bank ---------------------------------------------------------------------

    /// <summary>
    /// Borrows <paramref name="offer"/>'s amount. One loan at a time. The money is borrowed,
    /// not earned, so <see cref="AllTimeEarnings"/> is untouched. The caller finds the offer
    /// in the current window (<see cref="LoanOffers.Find"/>); an expired id never gets here.
    /// </summary>
    public Result<Loan> TakeLoan(LoanOffer offer, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(offer);
        if (ActiveLoan is not null) return Result.Fail<Loan>("You already have a loan. Repay it first.");
        if (offer.PrestigeLevel != PrestigeLevel || nowUtc >= offer.ValidUntil)
            return Result.Fail<Loan>("This offer has expired.");

        var loan = Loan.Create(Id, offer, nowUtc);
        _loans.Add(loan);
        Cash += offer.Amount;
        MarkUpdated();
        return Result.Ok(loan);
    }

    /// <summary>
    /// Collects every installment that has fallen due by <paramref name="nowUtc"/> — several
    /// after time away. Cash never goes negative: a shortfall is taken as far as cash allows,
    /// stays owed, and adds a <see cref="GameConstants.LoanPenaltyRate"/> penalty. Returns
    /// what happened, or null when nothing was due.
    /// </summary>
    public LoanCollection? CollectLoanPayments(DateTime nowUtc)
    {
        var loan = ActiveLoan;
        if (loan is null) return null;

        decimal paid = 0m, penalty = 0m;
        var collected = false;
        while (loan.IsActive && loan.NextPaymentAt is { } dueAt && dueAt <= nowUtc)
        {
            var due = loan.DueAmount;
            var pay = Math.Min(due, Cash);
            Cash -= pay;
            paid += pay;
            penalty += loan.Collect(due, pay, dueAt);
            collected = true;
        }

        if (!collected) return null;
        MarkUpdated();
        return new LoanCollection(loan, paid, penalty);
    }

    /// <summary>Pays off everything still owed, from cash.</summary>
    public Result<Loan> RepayLoan(DateTime nowUtc)
    {
        var loan = ActiveLoan;
        if (loan is null) return Result.Fail<Loan>("You have no loan to repay.");

        var deduct = DeductCash(loan.Outstanding);
        if (!deduct.IsSuccess) return Result.Fail<Loan>(deduct.Error!);

        loan.RepayAll(nowUtc);
        MarkUpdated();
        return Result.Ok(loan);
    }

    // -- Diamonds and the store (contract §6e) ----------------------------------

    public bool IsBoostedAt(DateTime atUtc) => BoostUntil is { } until && atUtc < until;

    /// <summary>Seconds of [<paramref name="fromUtc"/>, <paramref name="toUtc"/>] a boost covers.</summary>
    public decimal BoostedSecondsWithin(DateTime fromUtc, DateTime toUtc)
    {
        if (BoostUntil is not { } until || until <= fromUtc || toUtc <= fromUtc) return 0m;
        var end = toUtc < until ? toUtc : until;
        return (decimal)(end - fromUtc).TotalSeconds;
    }

    /// <summary>
    /// Buys <paramref name="hours"/> of × <see cref="GameConstants.BoostMultiplier"/> income.
    /// While a boost runs, the time is added to it — never multiplied again.
    /// </summary>
    public Result BuyBoost(int hours, DateTime nowUtc)
    {
        var option = GameConstants.BoostOptions.FirstOrDefault(o => o.Hours == hours);
        if (option.Hours == 0) return Result.Fail("Unknown boost.");

        var from = IsBoostedAt(nowUtc) ? BoostUntil!.Value : nowUtc;
        var until = from.AddHours(hours);
        if (until - nowUtc > GameConstants.MaxBoostAhead)
            return Result.Fail("A boost can run at most 24 hours ahead.");

        var paid = SpendDiamonds(option.Price, DiamondReasons.Boost, hours.ToString(CultureInfo.InvariantCulture), nowUtc);
        if (!paid.IsSuccess) return paid;

        BoostUntil = until;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>Called by <c>/state</c> after crediting time away: those earnings can be paid again.</summary>
    public void OfferOfflineDouble(decimal earned, DateTime nowUtc)
    {
        if (earned <= 0m) return;
        OfflineBonusAmount = earned;
        OfflineBonusUntil = nowUtc + GameConstants.OfflineDoubleWindow;
        MarkUpdated();
    }

    public bool HasOfflineDoubleAt(DateTime nowUtc) =>
        OfflineBonusAmount > 0m && OfflineBonusUntil is { } until && nowUtc <= until;

    /// <summary>Pays the last offline earnings a second time, once. It is income, so it counts as earnings.</summary>
    public Result DoubleOfflineEarnings(DateTime nowUtc)
    {
        if (!HasOfflineDoubleAt(nowUtc)) return Result.Fail("No offline earnings to double.");

        var paid = SpendDiamonds(GameConstants.OfflineDoublePrice, DiamondReasons.DoubleOffline, null, nowUtc);
        if (!paid.IsSuccess) return paid;

        AddCash(OfflineBonusAmount);
        OfflineBonusAmount = 0m;
        OfflineBonusUntil = null;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>
    /// Turns diamonds into cash at <see cref="GameConstants.DiamondCashValue"/>. One way only.
    /// Not income: <see cref="AllTimeEarnings"/> (the leaderboard) is untouched.
    /// </summary>
    public Result ExchangeDiamonds(int diamonds, DateTime nowUtc)
    {
        if (diamonds < 1) return Result.Fail("Choose at least 1 diamond.");

        var paid = SpendDiamonds(diamonds, DiamondReasons.Exchange, null, nowUtc);
        if (!paid.IsSuccess) return paid;

        Cash += diamonds * GameConstants.DiamondCashValue(PrestigeLevel);
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>Buys a badge for good. The first one bought becomes the featured badge.</summary>
    public Result BuyBadge(BadgeDefinition badge, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(badge);
        if (_badges.Any(b => b.BadgeId == badge.Id)) return Result.Fail("You already own this badge.");

        var paid = SpendDiamonds(badge.Price, DiamondReasons.Badge, badge.Id, nowUtc);
        if (!paid.IsSuccess) return paid;

        _badges.Add(CompanyBadge.Create(badge.Id, nowUtc));
        FeaturedBadgeId ??= badge.Id;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>Chooses the badge shown next to the name; null shows none.</summary>
    public Result FeatureBadge(string? badgeId)
    {
        if (badgeId is not null && _badges.All(b => b.BadgeId != badgeId))
            return Result.Fail("You do not own this badge.");

        FeaturedBadgeId = badgeId;
        MarkUpdated();
        return Result.Ok();
    }

    public bool OwnsAvatar(AvatarDefinition avatar) =>
        avatar.IsFree || _avatars.Any(a => a.AvatarId == avatar.Id);

    /// <summary>Buys an avatar for good and puts it on. Free ones need no buying.</summary>
    public Result BuyAvatar(AvatarDefinition avatar, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(avatar);
        if (OwnsAvatar(avatar)) return Result.Fail("You already own this avatar.");

        var paid = SpendDiamonds(avatar.Price, DiamondReasons.Avatar, avatar.Id, nowUtc);
        if (!paid.IsSuccess) return paid;

        _avatars.Add(CompanyAvatar.Create(avatar.Id, nowUtc));
        AvatarId = avatar.Id;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>Puts on an owned (or free) avatar; null goes back to the initial.</summary>
    public Result SelectAvatar(AvatarDefinition? avatar)
    {
        if (avatar is not null && !OwnsAvatar(avatar)) return Result.Fail("You do not own this avatar.");

        AvatarId = avatar?.Id;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>Admin: gives (positive) or takes away (negative) diamonds, with a note for the ledger.</summary>
    public Result AdminAdjustDiamonds(int amount, string? reason, DateTime nowUtc)
    {
        if (amount == 0) return Result.Fail("Amount cannot be zero.");
        if (Diamonds + (long)amount < 0) return Result.Fail("Diamonds cannot go below zero.");
        var note = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (note is { Length: > DiamondTransaction.MaxDetailLength })
            return Result.Fail("Reason must be at most 200 characters.");

        Record(amount, DiamondReasons.Admin, note, nowUtc);
        return Result.Ok();
    }

    private void GrantDiamonds(int amount, string reason, string? detail, DateTime nowUtc)
    {
        if (amount > 0) Record(amount, reason, detail, nowUtc);
    }

    private Result SpendDiamonds(int amount, string reason, string? detail, DateTime nowUtc)
    {
        if (amount > Diamonds) return Result.Fail("Not enough diamonds.");
        Record(-amount, reason, detail, nowUtc);
        return Result.Ok();
    }

    /// <summary>The only place the balance changes — always with its ledger line.</summary>
    private void Record(int amount, string reason, string? detail, DateTime nowUtc)
    {
        Diamonds += amount;
        _diamondLedger.Add(DiamondTransaction.Create(Id, amount, Diamonds, reason, detail, nowUtc));
        MarkUpdated();
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

        foreach (var a in fresh)
        {
            _achievements.Add(CompanyAchievement.Create(a.Code, nowUtc));
            GrantDiamonds(GameConstants.AchievementDiamonds, DiamondReasons.Achievement, a.Code, nowUtc);
        }
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
        _loans.Clear();
        Cash = 0m;
        AllTimeEarnings = 0m;
        TaxesPaid = 0m;   // the businesses, and their bills, are gone too
        PrestigeLevel = PrestigeLevel.TheHustle;
        PrestigeCount = 0;
        PassiveIncomePerSecond = GameConstants.BasePassiveIncomePerSecond;
        BoostUntil = null;
        OfflineBonusAmount = 0m;
        OfflineBonusUntil = null;
        // Diamonds and badges are kept: they may one day be paid for.
        LastSyncAt = nowUtc;
        CashOverridePending = true;
        MarkUpdated();
    }

    /// <summary>
    /// Admin: cancels what is still owed on the active loan. Cash is untouched; the loan is
    /// closed as forgiven, so the player may take a new one.
    /// </summary>
    public Result AdminForgiveLoan(DateTime nowUtc)
    {
        var loan = ActiveLoan;
        if (loan is null) return Result.Fail("Player has no active loan.");

        loan.Forgive(nowUtc);
        MarkUpdated();
        return Result.Ok();
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
        // Otherwise closing would be a way out of a tax bill.
        if (biz.TaxDue > 0m) return Result.Fail("Pay this business's taxes before closing it.");

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
        if (TaxesDue > 0m)
            return Result.Fail("Pay your taxes before prestige.");
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
        GrantDiamonds(GameConstants.PrestigeDiamondsPerLevel * (int)PrestigeLevel, DiamondReasons.Prestige,
            PrestigeLevel.ToString(), nowUtc);

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
