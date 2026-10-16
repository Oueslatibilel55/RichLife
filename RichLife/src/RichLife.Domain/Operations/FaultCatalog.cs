using RichLife.Domain.Enums;

namespace RichLife.Domain.Operations;

/// <summary>
/// One way a unit can break. A repair costs <c>max(MinCost, unit price × CostShare)</c> and
/// takes <see cref="RepairHours"/>; <see cref="Weight"/> is how often it is drawn against the
/// others of the same business.
/// </summary>
public sealed record FaultDefinition(
    string Id, string Name, decimal CostShare, decimal MinCost, int RepairHours, int Weight)
{
    public decimal RepairCost(decimal unitPrice) =>
        Math.Round(Math.Max(MinCost, unitPrice * CostShare), 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// What can break, per business type (features/014-business-places-and-equipment.md). Rules in
/// code, like the banks: the fault of a unit is never stored — it is drawn again from the unit's
/// id and repair count — so <b>fault ids are part of the contract (the client translates them):
/// never rename or reuse one</b>, and keep each list's order, or a broken unit's fault changes.
/// </summary>
public static class FaultCatalog
{
    private static readonly FaultDefinition[] Vehicle =
    [
        new("flat-tire",   "Flat tyre",          0.15m,  40m, 1, 30),
        new("battery",     "Dead battery",       0.20m,  90m, 2, 20),
        new("brakes",      "Worn-out brakes",    0.35m, 150m, 3, 15),
        new("electrical",  "Electrical fault",   0.40m, 200m, 4, 10),
        new("overheating", "Engine overheating", 0.50m, 250m, 5, 12),
        new("clutch",      "Clutch failure",     0.70m, 400m, 8, 6),
        new("gearbox",     "Gearbox failure",    0.90m, 600m, 12, 4),
        new("engine",      "Engine failure",     1.20m, 900m, 24, 2),
    ];

    // Shared lists for the catalogue of features/015 (several businesses each).
    private static readonly FaultDefinition[] Office =
    [
        new("it-outage",      "IT outage",       0.20m, 500m, 2, 30),
        new("key-staff-quit", "Key staff quit",  0.30m, 800m, 6, 15),
        new("client-dispute", "Client dispute",  0.15m, 400m, 3, 20),
    ];

    private static readonly FaultDefinition[] Finance =
    [
        new("system-glitch",    "Trading system glitch", 0.20m,   600m, 2, 30),
        new("compliance-audit", "Compliance audit",      0.15m, 1_000m, 3, 25),
        new("market-crash",     "Market crash",          0.25m,   500m, 4, 15),
    ];

    private static readonly FaultDefinition[] Energy =
    [
        new("grid-fault",        "Grid fault",        0.10m,   500m, 3, 30),
        new("generator-failure", "Generator failure", 0.25m, 1_000m, 6, 20),
        new("safety-inspection", "Safety inspection", 0.08m,   800m, 4, 20),
    ];

    private static readonly FaultDefinition[] Aircraft =
    [
        new("bird-strike",       "Bird strike",       0.05m,  5_000m,  6, 20),
        new("avionics-fault",    "Avionics fault",    0.08m,  8_000m,  8, 20),
        new("engine-inspection", "Engine inspection", 0.10m, 10_000m, 12, 15),
        new("landing-gear",      "Landing gear",      0.12m, 15_000m, 18, 10),
    ];

    private static readonly FaultDefinition[] Ship =
    [
        new("port-strike", "Port strike",        0.03m, 10_000m,  8, 25),
        new("engine-room", "Engine room fault",  0.08m, 15_000m, 12, 20),
        new("hull-damage", "Hull damage",        0.10m, 20_000m, 24, 10),
    ];

    private static readonly FaultDefinition[] Space =
    [
        new("launch-scrub", "Launch scrubbed",     0.03m,   500_000m,  6, 30),
        new("life-support", "Life support fault", 0.10m, 1_000_000m, 24, 15),
        new("heat-shield",  "Heat shield damage", 0.15m, 2_000_000m, 24, 10),
    ];

    private static readonly FaultDefinition[] Clinic =
    [
        new("scanner-down",   "Scanner down",   0.30m, 2_000m, 6, 25),
        new("staff-shortage", "Staff shortage", 0.10m, 1_000m, 4, 25),
    ];

    private static readonly FaultDefinition[] Showroom =
    [
        new("car-damaged",   "Car damaged on the lot", 0.05m, 1_500m, 4, 25),
        new("recall-notice", "Recall notice",          0.10m, 2_000m, 6, 15),
    ];

    private static readonly FaultDefinition[] Kiosk =
    [
        new("parts-shortage", "Parts shortage", 0.30m, 50m, 2, 30),
        new("broken-tool",    "Broken tool",    0.20m, 30m, 1, 25),
    ];

    private static readonly FaultDefinition[] Hyperloop =
    [
        new("pod-fault",        "Pod fault",        0.10m, 1_000_000m,  6, 25),
        new("track-inspection", "Track inspection", 0.05m, 2_000_000m, 12, 15),
    ];

    private static readonly Dictionary<string, FaultDefinition[]> ByBusiness = new()
    {
        // features/015 — the businesses that were added or had no list of their own.
        ["delivery-company"] = Vehicle,
        ["bus-line"] = Vehicle,
        ["logistics-hub"] = Vehicle,
        ["cargo-airline"] = Aircraft,
        ["airline-group"] = Aircraft,
        ["cruise-line"] = Ship,
        ["container-shipping"] = Ship,
        ["space-tourism"] = Space,
        ["mars-colony"] = Space,
        ["hyperloop"] = Hyperloop,
        ["phone-repair"] = Kiosk,
        ["car-dealership"] = Showroom,
        ["private-clinic"] = Clinic,
        ["rooftop-solar"] = Energy,
        ["wind-farm"] = Energy,
        ["solar-park"] = Energy,
        ["nuclear-plant"] = Energy,
        ["oil-major"] = Energy,
        ["fusion-grid"] = Energy,
        ["penny-stocks"] = Finance,
        ["trading-desk"] = Finance,
        ["investment-fund"] = Finance,
        ["stock-brokerage"] = Finance,
        ["investment-bank"] = Finance,
        ["global-bank"] = Finance,
        ["world-exchange"] = Finance,
        ["web-agency"] = Office,
        ["software-startup"] = Office,
        ["tech-unicorn"] = Office,
        ["streaming-platform"] = Office,
        ["ai-superlab"] = Office,
        ["metaverse"] = Office,
        ["consulting-firm"] = Office,
        ["global-consultancy"] = Office,

        ["food-cart"] =
        [
            new("burner-out",     "Gas burner out",    0.20m,  30m, 1, 30),
            new("fridge-failure", "Fridge failure",    0.40m,  50m, 3, 20),
            new("permit-check",   "Permit check fine", 0.25m,  40m, 1, 15),
        ],
        ["bike-courier"] =
        [
            new("flat-tyre",    "Flat tyre",           0.15m,  15m, 1, 35),
            new("broken-chain", "Broken chain",        0.20m,  20m, 1, 25),
            new("bike-battery", "E-bike battery dead", 0.40m,  60m, 3, 15),
            new("stolen-bike",  "Bike stolen",         1.00m, 200m, 8, 5),
        ],
        ["car-wash"] =
        [
            new("pump-failure", "Pump failure", 0.30m, 120m, 3, 30),
            new("worn-brushes", "Worn brushes", 0.20m,  80m, 2, 30),
            new("water-cut",    "Water cut",    0.10m,  60m, 4, 15),
        ],
        ["parking-lot"] =
        [
            new("barrier-stuck",  "Barrier stuck open",   0.25m, 150m, 2, 30),
            new("ticket-machine", "Ticket machine down",  0.35m, 200m, 3, 25),
            new("damage-claim",   "Damage claim",         0.30m, 250m, 1, 15),
        ],
        ["vending"] =
        [
            new("coin-jam",     "Coin jam",         0.15m,  40m, 1, 35),
            new("card-reader",  "Card reader down", 0.30m,  80m, 2, 25),
            new("vandalism",    "Vandalised",       0.80m, 200m, 5, 10),
            new("out-of-stock", "Empty — restock",  0.20m,  60m, 1, 30),
        ],
        ["mini-market"] =
        [
            new("fridge-breakdown", "Fridge breakdown (stock lost)", 0.40m, 300m, 4, 30),
            new("power-cut",        "Power cut",                     0.15m, 200m, 2, 20),
            new("shoplifting",      "Shoplifting",                   0.25m, 150m, 1, 20),
        ],
        ["laundromat"] =
        [
            new("drum-failure",   "Drum failure",   0.50m, 120m, 4, 25),
            new("door-lock",      "Door lock",      0.20m,  40m, 1, 30),
            new("heater-failure", "Heater failure", 0.40m,  90m, 3, 25),
        ],
        ["taxi-fleet"] = Vehicle,
        ["transport-company"] = Vehicle,
        ["rent-a-car"] =
        [
            .. Vehicle,
            new("returned-damaged", "Returned damaged", 0.40m, 300m, 4, 15),
        ],
        ["gym"] =
        [
            new("machine-out",      "Out of order",     0.35m, 150m, 3, 35),
            new("air-conditioning", "Air conditioning", 0.30m, 400m, 4, 15),
        ],
        ["restaurant-chain"] =
        [
            new("oven-breakdown",    "Oven breakdown",    0.35m, 800m, 4, 25),
            new("cold-room-failure", "Cold room failure", 0.35m, 600m, 4, 20),
            new("health-inspection", "Health inspection", 0.20m, 500m, 2, 15),
            new("bad-review",        "Bad review",        0.10m, 300m, 3, 10),
        ],
        ["small-hotel"] =
        [
            new("room-ac",  "Air conditioning", 0.40m, 300m, 3, 30),
            new("plumbing", "Plumbing leak",    0.35m, 250m, 2, 30),
        ],
        ["fuel-station"] =
        [
            new("pump-out",   "Out of order",                   0.20m, 1_500m, 3, 30),
            new("tank-empty", "Tank empty — fuel truck late",  0.10m, 1_000m, 2, 20),
        ],
        ["real-estate"] =
        [
            new("boiler",      "Boiler breakdown", 0.06m, 800m, 4, 25),
            new("leak",        "Water leak",       0.05m, 600m, 3, 30),
            new("late-tenant", "Tenant not paying", 0.04m, 500m, 6, 15),
        ],
        ["shopping-mall"] =
        [
            new("escalator",   "Escalator down",   0.05m, 2_000m, 4, 20),
            new("mall-ac",     "Air conditioning", 0.05m, 3_000m, 4, 20),
            new("shop-closed", "Tenant left",      0.10m, 5_000m, 8, 15),
        ],
        ["office-tower"] =
        [
            new("lift-down", "Lift down",                0.02m, 5_000m, 4, 25),
            new("tower-ac",  "Power or air conditioning", 0.02m, 8_000m, 4, 20),
        ],
        ["airport"] =
        [
            new("runway-maintenance", "Runway maintenance",  0.01m, 100_000m, 4, 20),
            new("baggage-system",     "Baggage system down", 0.02m,  80_000m, 3, 25),
            new("storm-strike",       "Storm or strike",     0.01m,  50_000m, 6, 15),
        ],
    };

    /// <summary>For a business type the list above does not know (one an admin added).</summary>
    private static readonly FaultDefinition[] Generic =
    [
        new("breakdown", "Breakdown", 0.30m, 50m, 3, 1),
    ];

    /// <summary>The faults a unit of this business type can suffer, in a fixed order.</summary>
    public static IReadOnlyList<FaultDefinition> For(string businessCatalogueId, BusinessSector sector) =>
        ByBusiness.TryGetValue(Aliases.GetValueOrDefault(businessCatalogueId, businessCatalogueId), out var list) ? list
        : sector == BusinessSector.Transport ? Vehicle
        : Generic;

    /// <summary>Businesses that break like another one (a coffee shop like a restaurant).</summary>
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["coffee-shop"] = "restaurant-chain",
        ["luxury-resort"] = "small-hotel",
        ["hotel-empire"] = "small-hotel",
        ["global-resorts"] = "small-hotel",
        ["skyscraper-district"] = "office-tower",
        ["megacity"] = "office-tower",
    };

    /// <summary>Every fault, once (a list shared by several businesses counts once).</summary>
    public static IEnumerable<FaultDefinition> All =>
        ByBusiness.Values.SelectMany(l => l).Concat(Vehicle).Concat(Generic).Distinct();
}
