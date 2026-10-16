namespace RichLife.Domain.Entities;

/// <summary>
/// Shares of one Bourse stock the company holds (features/017-bourse.md). Owned by
/// <see cref="Company"/>, keyed by (company, ticker). <see cref="CostBasis"/> is what the shares
/// still held cost, fees included — selling takes its share out proportionally.
/// </summary>
public sealed class CompanyStock
{
    public string Ticker { get; private set; } = string.Empty;
    public long Shares { get; private set; }
    public decimal CostBasis { get; private set; }

    private CompanyStock() { }

    internal static CompanyStock Create(string ticker) => new() { Ticker = ticker };

    internal void Add(long shares, decimal cost)
    {
        Shares += shares;
        CostBasis += cost;
    }

    /// <summary>Removes shares and returns the part of the cost basis they carried.</summary>
    internal decimal Remove(long shares)
    {
        var part = Shares == shares ? CostBasis : Math.Round(CostBasis * shares / Shares, 2, MidpointRounding.AwayFromZero);
        Shares -= shares;
        CostBasis -= part;
        return part;
    }
}
