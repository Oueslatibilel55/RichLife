using RichLife.Domain.Common;

namespace RichLife.Domain.Market;

/// <summary>
/// Prices on the Bourse (features/017-bourse.md). Never stored and never simulated by a job: a
/// price is a pure function of the stock and the minute — a few sine waves of different lengths
/// (2 h, 9 h, 2.5 days, 11 days) with seeded phases, plus smooth seeded noise — so every player
/// sees the same market, a chart is just the function sampled, and the price only depends on
/// the server clock. Prices move around <see cref="StockDefinition.BasePrice"/> (mean-reverting)
/// and step every <see cref="GameConstants.StockTickSeconds"/> seconds.
/// </summary>
public static class StockMarket
{
    private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Wave lengths in minutes and their weights.</summary>
    private static readonly (double Minutes, double Weight)[] Waves =
        [(120, 0.12), (540, 0.25), (3_600, 0.60), (15_840, 0.50)];

    private const double NoiseWeight = 0.10;
    private const int NoiseStepMinutes = 10;

    /// <summary>
    /// The price in force at <paramref name="atUtc"/>. Prices step every
    /// <see cref="GameConstants.StockTickSeconds"/> seconds (aligned on the epoch), so what a
    /// player sees is exactly what a trade in the same tick pays.
    /// </summary>
    public static decimal PriceAt(StockDefinition stock, DateTime atUtc)
    {
        var minutes = (TickStart(atUtc) - Epoch).TotalMinutes;
        var seed = TickerSeed(stock.Ticker);

        var phases = new SeededRandom(seed);
        var wave = 0.0;
        foreach (var (length, weight) in Waves)
            wave += weight * Math.Sin(2 * Math.PI * minutes / length + (double)phases.NextUnit() * 2 * Math.PI);

        var step = Math.Floor(minutes / NoiseStepMinutes);
        var f = minutes / NoiseStepMinutes - step;
        var smooth = f * f * (3 - 2 * f);
        var noise = Noise(seed, (long)step) * (1 - smooth) + Noise(seed, (long)step + 1) * smooth;

        var log = Math.Log((double)stock.BasePrice) + stock.Volatility * (wave + NoiseWeight * noise);
        return Math.Round((decimal)Math.Exp(log), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Start of the price tick that contains <paramref name="atUtc"/>.</summary>
    public static DateTime TickStart(DateTime atUtc)
    {
        var tick = TimeSpan.FromSeconds(GameConstants.StockTickSeconds).Ticks;
        var since = (atUtc - Epoch).Ticks;
        return Epoch.AddTicks(since - ((since % tick) + tick) % tick);
    }

    /// <summary>When the next price tick starts.</summary>
    public static DateTime NextTickAt(DateTime atUtc) => TickStart(atUtc).AddSeconds(GameConstants.StockTickSeconds);

    /// <summary>
    /// Shares traded over the last 24 hours — flavour for the quote table, seeded per stock and
    /// hour so it is stable within the hour and the same for everyone. Not used by any rule.
    /// </summary>
    public static long Volume24h(StockDefinition stock, DateTime atUtc)
    {
        var hour = (long)Math.Floor((atUtc - Epoch).TotalHours);
        var baseVolume = 4_000_000d / (double)stock.BasePrice * (1 + stock.Volatility * 10);
        var wobble = 0.6 + 0.8 * (Noise(TickerSeed(stock.Ticker) ^ 0xA5A5A5A5UL, hour) + 1) / 2;
        return (long)Math.Round(baseVolume * wobble, 0);
    }

    /// <summary>Change since 24 hours earlier, as a fraction (0.034 = +3.4 %).</summary>
    public static decimal Change24h(StockDefinition stock, DateTime atUtc)
    {
        var before = PriceAt(stock, atUtc.AddHours(-24));
        return before == 0m ? 0m : Math.Round((PriceAt(stock, atUtc) - before) / before, 4);
    }

    /// <summary><paramref name="count"/> prices every <paramref name="step"/>, ending at <paramref name="endUtc"/>.</summary>
    public static IReadOnlyList<(DateTime At, decimal Price)> History(
        StockDefinition stock, DateTime endUtc, TimeSpan step, int count)
    {
        var points = new List<(DateTime, decimal)>(count);
        for (var i = count - 1; i >= 0; i--)
        {
            var at = endUtc - step * i;
            points.Add((at, PriceAt(stock, at)));
        }
        return points;
    }

    /// <summary>−1 ≤ n &lt; 1, the same for a given stock and step forever.</summary>
    private static double Noise(ulong seed, long step) =>
        (double)new SeededRandom(seed ^ (ulong)step * 0x9E3779B97F4A7C15UL).NextUnit() * 2 - 1;

    /// <summary>FNV-1a — stable across runs and .NET versions, unlike string.GetHashCode.</summary>
    private static ulong TickerSeed(string ticker)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in ticker.ToUpperInvariant())
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return hash;
    }
}
