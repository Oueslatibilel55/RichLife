namespace RichLife.Domain.Banking;

/// <summary>
/// A lender. Its rate and term bands shape the offers it makes (<see cref="LoanOffers"/>):
/// cheap banks lend short, expensive ones lend long. Fictional names on purpose.
/// </summary>
public sealed record Bank(
    string Id,
    string Name,
    string Icon,
    decimal MinRate,
    decimal MaxRate,
    int MinInstallments,
    int MaxInstallments);

/// <summary>
/// The 20 banks, defined in code like the achievements: they are rules (rate bands), not
/// content an admin edits. <b>Ids are persisted on loans — never rename or reuse one.</b>
/// </summary>
public static class BankCatalog
{
    public static readonly IReadOnlyList<Bank> All =
    [
        new("atlas-bank",        "Atlas Bank",        "🏛️", 0.03m, 0.06m, 4, 8),
        new("carthage-credit",   "Carthage Credit",   "🏺", 0.04m, 0.08m, 4, 10),
        new("meridian-trust",    "Meridian Trust",    "🧭", 0.03m, 0.07m, 6, 12),
        new("northwind-savings", "Northwind Savings", "🌬️", 0.05m, 0.09m, 6, 12),
        new("goldleaf-bank",     "Goldleaf Bank",     "🍂", 0.04m, 0.07m, 4, 8),
        new("harbor-and-co",     "Harbor & Co.",      "⚓", 0.06m, 0.10m, 8, 16),
        new("summit-capital",    "Summit Capital",    "🏔️", 0.05m, 0.09m, 6, 12),
        new("oasis-finance",     "Oasis Finance",     "🌴", 0.07m, 0.12m, 8, 16),
        new("ironclad-lending",  "Ironclad Lending",  "🛡️", 0.08m, 0.14m, 10, 16),
        new("sterling-union",    "Sterling Union",    "💷", 0.03m, 0.06m, 4, 6),
        new("nova-bank",         "Nova Bank",         "✨", 0.06m, 0.11m, 6, 12),
        new("cedar-mutual",      "Cedar Mutual",      "🌲", 0.04m, 0.08m, 6, 10),
        new("falcon-credit",     "Falcon Credit",     "🦅", 0.09m, 0.15m, 4, 8),
        new("lighthouse-bank",   "Lighthouse Bank",   "🔦", 0.05m, 0.08m, 6, 10),
        new("medina-bank",       "Medina Bank",       "🕌", 0.04m, 0.09m, 8, 14),
        new("pioneer-trust",     "Pioneer Trust",     "🧗", 0.07m, 0.13m, 10, 16),
        new("silverline-bank",   "Silverline Bank",   "🥈", 0.05m, 0.10m, 6, 12),
        new("crescent-finance",  "Crescent Finance",  "🌙", 0.06m, 0.12m, 8, 14),
        new("titan-capital",     "Titan Capital",     "🗿", 0.08m, 0.15m, 12, 16),
        new("jasmine-bank",      "Jasmine Bank",      "🌼", 0.04m, 0.08m, 4, 10),
    ];

    public static Bank? Find(string id) => All.FirstOrDefault(b => b.Id == id);
}
