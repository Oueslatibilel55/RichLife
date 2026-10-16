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

    private static readonly Dictionary<string, FaultDefinition[]> ByBusiness = new()
    {
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
        ByBusiness.TryGetValue(businessCatalogueId, out var list) ? list
        : sector == BusinessSector.Transport ? Vehicle
        : Generic;

    /// <summary>Every fault, once (a list shared by several businesses counts once).</summary>
    public static IEnumerable<FaultDefinition> All =>
        ByBusiness.Values.SelectMany(l => l).Concat(Vehicle).Concat(Generic).Distinct();
}
