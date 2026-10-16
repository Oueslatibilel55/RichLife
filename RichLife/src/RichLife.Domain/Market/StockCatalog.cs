namespace RichLife.Domain.Market;

/// <summary>
/// A listed company (features/017-bourse.md). <see cref="Volatility"/> scales how far its price
/// swings around <see cref="BasePrice"/>.
/// </summary>
public sealed record StockDefinition(
    string Ticker, string Name, string Sector, string Icon, decimal BasePrice, double Volatility);

/// <summary>
/// The stocks on the Bourse — rules in code, like the banks. Fictional companies: no real
/// brand's price is ever shown. <b>Tickers are persisted (holdings): never rename or reuse one.</b>
/// </summary>
public static class StockCatalog
{
    public static readonly IReadOnlyList<StockDefinition> All =
    [
        new("CRTH", "Carthage Motors",  "Transport",   "🚗", 42m,  0.09),
        new("MDTL", "Medina Telecom",   "Technology",  "📡", 18m,  0.06),
        new("SAHL", "Sahel Foods",      "Food",        "🫒", 25m,  0.05),
        new("DJBA", "Djerba Resorts",   "Tourism",     "🏝️", 60m,  0.10),
        new("OLVA", "Olive Gold",       "Agriculture", "🌿", 12m,  0.07),
        new("SFAX", "Sfax Shipping",    "Transport",   "🚢", 33m,  0.08),
        new("ATLS", "Atlas Energy",     "Energy",      "⚡", 75m,  0.09),
        new("NOVA", "Nova AI",          "Technology",  "🤖", 140m, 0.16),
        new("ORBT", "Orbit Space",      "Aerospace",   "🚀", 220m, 0.18),
        new("GRNE", "Green Grid",       "Energy",      "🔋", 55m,  0.11),
        new("PIXL", "Pixel Games",      "Technology",  "🎮", 30m,  0.13),
        new("LUXE", "Luxe Brands",      "Retail",      "👜", 180m, 0.07),
    ];

    public static StockDefinition? Find(string ticker) =>
        All.FirstOrDefault(s => string.Equals(s.Ticker, ticker, StringComparison.OrdinalIgnoreCase));
}
