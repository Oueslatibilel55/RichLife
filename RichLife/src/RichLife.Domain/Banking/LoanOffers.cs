using System.Globalization;
using RichLife.Domain.Enums;

namespace RichLife.Domain.Banking;

/// <summary>A loan a bank proposes right now. Valid until <see cref="ValidUntil"/>.</summary>
public sealed record LoanOffer(
    string Id,
    Bank Bank,
    PrestigeLevel PrestigeLevel,
    decimal Amount,
    decimal InterestRate,
    int Installments,
    DateTime ValidUntil)
{
    public decimal TotalRepay => decimal.Round(Amount * (1m + InterestRate), 2);
    /// <summary>Rounded up to the cent, so the last installment is the smaller one, never a stray extra.</summary>
    public decimal InstallmentAmount => Math.Ceiling(TotalRepay * 100m / Installments) / 100m;
}

/// <summary>
/// The offers on the table. They are not stored: each 6-hour window
/// (<see cref="GameConstants.LoanOfferRotation"/>, aligned on midnight UTC) has its own seed,
/// so the same level sees the same <see cref="GameConstants.LoanOffersPerLevel"/> offers all
/// window long, for every player, and a fresh set in the next one. Taking an offer simply
/// regenerates the current set and looks the id up — an id from a past window is expired.
/// </summary>
public static class LoanOffers
{
    public static IReadOnlyList<LoanOffer> For(PrestigeLevel level, DateTime nowUtc)
    {
        var window = WindowIndex(nowUtc);
        var validUntil = WindowStart(window + 1);
        var (min, max) = GameConstants.LoanAmountRange(level);
        var rng = new SeededRandom(Seed(window, level));

        // A seeded shuffle picks distinct banks for the window.
        var banks = BankCatalog.All.ToArray();
        for (var i = banks.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (banks[i], banks[j]) = (banks[j], banks[i]);
        }

        return banks
            .Take(GameConstants.LoanOffersPerLevel)
            .Select((bank, slot) => new LoanOffer(
                Id: string.Create(CultureInfo.InvariantCulture, $"{window}-{(int)level}-{slot}"),
                Bank: bank,
                PrestigeLevel: level,
                Amount: RoundAmount(LogUniform(rng, min, max)),
                InterestRate: RoundRate(bank.MinRate + (bank.MaxRate - bank.MinRate) * rng.NextUnit()),
                Installments: bank.MinInstallments + 2 * rng.Next((bank.MaxInstallments - bank.MinInstallments) / 2 + 1),
                ValidUntil: validUntil))
            .ToList();
    }

    /// <summary>The offer with this id in the current window, or null (unknown or expired).</summary>
    public static LoanOffer? Find(string offerId, PrestigeLevel level, DateTime nowUtc) =>
        For(level, nowUtc).FirstOrDefault(o => o.Id == offerId);

    /// <summary>When the current offers are replaced.</summary>
    public static DateTime NextRotation(DateTime nowUtc) => WindowStart(WindowIndex(nowUtc) + 1);

    private static long WindowIndex(DateTime nowUtc) =>
        nowUtc.Ticks / GameConstants.LoanOfferRotation.Ticks;

    private static DateTime WindowStart(long window) =>
        new(window * GameConstants.LoanOfferRotation.Ticks, DateTimeKind.Utc);

    private static ulong Seed(long window, PrestigeLevel level) =>
        (ulong)window * 0x9E3779B97F4A7C15UL ^ ((ulong)(int)level + 1) * 0xBF58476D1CE4E5B9UL;

    // Spread over orders of magnitude: a 5k–40k range gives small and large loans alike.
    private static decimal LogUniform(SeededRandom rng, decimal min, decimal max)
    {
        var lo = Math.Log((double)min);
        var hi = Math.Log((double)max);
        return (decimal)Math.Exp(lo + (hi - lo) * (double)rng.NextUnit());
    }

    /// <summary>Two significant digits: 37,412 → 37,000; 1,284,000 → 1,300,000.</summary>
    private static decimal RoundAmount(decimal amount)
    {
        var magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)amount)) - 1);
        return decimal.Round(amount / magnitude, 0) * magnitude;
    }

    /// <summary>Half a percent steps: 0.0637 → 0.065.</summary>
    private static decimal RoundRate(decimal rate) => decimal.Round(rate * 200m, 0) / 200m;

    /// <summary>
    /// SplitMix64. <see cref="Random"/>'s seeded sequence is not guaranteed to stay the same
    /// across .NET versions, and offers must not change when the runtime is upgraded mid-window.
    /// </summary>
    private sealed class SeededRandom(ulong seed)
    {
        private ulong _state = seed;

        private ulong NextULong()
        {
            var z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>0 ≤ n &lt; <paramref name="maxExclusive"/>.</summary>
        public int Next(int maxExclusive) => (int)(NextULong() % (ulong)maxExclusive);

        /// <summary>0 ≤ x &lt; 1.</summary>
        public decimal NextUnit() => (decimal)(NextULong() >> 11) / (1UL << 53);
    }
}
