using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <summary>
    /// A catalogue that plays like Road to Rich (features/015-catalogue-like-road-to-rich.md):
    /// 58 businesses, 8–9 per prestige level, each a fleet, a site chain or an equipment business,
    /// with slower paybacks. Every player's progress is wiped first — accounts and company names
    /// stay, everything else restarts as a new company (25 welcome diamonds).
    /// </summary>
    /// <remarks>
    /// Incomes are not typed by hand: each one is the price divided by the prestige level's
    /// payback time (<see cref="Payback"/>), so the whole economy is tuned from one table.
    /// </remarks>
    public partial class CatalogueLikeRoadToRich : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseIncomePerSecond", table: "catalogue_place_kinds",
                type: "numeric(20,6)", precision: 20, scale: 6, nullable: true);

            // 3 = Equipment, the default for entries this migration does not know.
            migrationBuilder.AddColumn<int>(
                name: "Kind", table: "catalogue_businesses", type: "integer", nullable: false, defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "Kind", table: "businesses", type: "integer", nullable: false, defaultValue: 3);

            // -- 1. Wipe every player's progress (the owner's call, 2026-10-16) ------------
            // Logins and companies (their names) stay; everything else is gone, diamonds
            // included, and each company restarts exactly like a new one. CashOverridePending
            // makes an open client adopt the server's figure on its next sync.
            migrationBuilder.Sql("""
                DELETE FROM businesses;
                DELETE FROM loans;
                DELETE FROM "LuxuryAssets";
                DELETE FROM "Assets";
                DELETE FROM company_achievements;
                DELETE FROM company_badges;
                DELETE FROM company_avatars;
                DELETE FROM diamond_transactions;

                UPDATE companies SET
                    "Cash" = 0, "AllTimeEarnings" = 0, "TaxesPaid" = 0,
                    "PrestigeLevel" = 1, "PrestigeCount" = 0, "PassiveIncomePerSecond" = 3,
                    "Diamonds" = 25, "BoostUntil" = NULL, "OfflineBonusAmount" = 0, "OfflineBonusUntil" = NULL,
                    "FeaturedBadgeId" = NULL, "AvatarId" = NULL,
                    "LastSyncAt" = now(), "CashOverridePending" = true, "UpdatedAt" = now();

                INSERT INTO diamond_transactions ("Id", "CompanyId", "Amount", "Balance", "Reason", "Detail", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), c."Id", 25, 25, 'welcome', NULL, now(), now() FROM companies c;
                """);

            // -- 2. The catalogue ---------------------------------------------------------
            var sql = new StringBuilder();
            foreach (var b in Catalogue) AppendBusiness(sql, b);
            migrationBuilder.Sql(sql.ToString());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The wipe cannot be undone; the content stays (it is data).
            migrationBuilder.DropColumn(name: "BaseIncomePerSecond", table: "catalogue_place_kinds");
            migrationBuilder.DropColumn(name: "Kind", table: "catalogue_businesses");
            migrationBuilder.DropColumn(name: "Kind", table: "businesses");
        }

        // -- Pacing ------------------------------------------------------------------------

        /// <summary>Seconds a purchase takes to pay for itself, by prestige level (1..7).</summary>
        private static readonly decimal[] Payback = [0, 1_800, 3_600, 7_200, 14_400, 21_600, 36_000, 54_000];

        private static decimal TierMultiplier(int tier) => tier switch { 2 => 1.5m, 3 => 2.2m, _ => 1m };

        /// <summary>Share of a place's price it earns back on its own: a site earns by itself, a depot hardly.</summary>
        private static decimal SelfShare(int kind) => kind switch { 1 => 0.25m, 2 => 1m, _ => 0.4m };

        private static (int Base, int PerLevel) Capacity(int kind) => kind switch { 1 => (3, 2), 2 => (2, 1), _ => (4, 2) };

        private static void AppendBusiness(StringBuilder sql, Biz b)
        {
            var payback = Payback[b.Prestige];
            decimal KindIncome(K k) =>
                Math.Round(k.Price / (payback * TierMultiplier(k.Tier)) * SelfShare(b.Kind), 4);
            var (capBase, capLevel) = b.Capacity ?? Capacity(b.Kind);
            var baseIncome = KindIncome(b.Kinds[0]);
            var opening = b.Kinds[0].Price;

            sql.Append(CultureInfo.InvariantCulture, $"""
                INSERT INTO catalogue_businesses
                    ("Id", "Name", "Sector", "RequiredPrestige", "OpeningCost", "BaseIncomePerSecond", "MonthlySalaryCost",
                     "BaseEmployeeCount", "Description", "DisplayOrder", "IsActive", "CreatedAt", "UpdatedAt",
                     "PlaceBaseCapacity", "PlaceCapacityPerLevel", "Kind")
                VALUES ('{b.Id}', {Str(b.Name)}, {b.Sector}, {b.Prestige}, {opening}, {baseIncome}, 0, 0, {Str(b.Description)},
                        {b.Order}, true, now(), now(), {capBase}, {capLevel}, {b.Kind})
                ON CONFLICT ("Id") DO UPDATE SET
                    "Name" = EXCLUDED."Name", "Sector" = EXCLUDED."Sector", "RequiredPrestige" = EXCLUDED."RequiredPrestige",
                    "OpeningCost" = EXCLUDED."OpeningCost", "BaseIncomePerSecond" = EXCLUDED."BaseIncomePerSecond",
                    "MonthlySalaryCost" = 0, "BaseEmployeeCount" = 0, "Description" = EXCLUDED."Description",
                    "DisplayOrder" = EXCLUDED."DisplayOrder", "IsActive" = true, "UpdatedAt" = now(),
                    "PlaceBaseCapacity" = EXCLUDED."PlaceBaseCapacity", "PlaceCapacityPerLevel" = EXCLUDED."PlaceCapacityPerLevel",
                    "Kind" = EXCLUDED."Kind";
                DELETE FROM catalogue_assets WHERE "BusinessCatalogueId" = '{b.Id}';
                DELETE FROM catalogue_place_kinds WHERE "BusinessCatalogueId" = '{b.Id}';

                """);

            for (var i = 0; i < b.Kinds.Length; i++)
            {
                var k = b.Kinds[i];
                sql.Append(CultureInfo.InvariantCulture,
                    $"""INSERT INTO catalogue_place_kinds ("BusinessCatalogueId", "Id", "Name", "Tier", "Price", "DisplayOrder", "BaseIncomePerSecond") VALUES ('{b.Id}', '{k.Id}', {Str(k.Name)}, {k.Tier}, {k.Price}, {(i + 1) * 10}, {KindIncome(k)});""")
                   .AppendLine();
            }

            // Within a business the dearer units pay back a little slower (× 0.9 … × 1.1).
            var units = b.Units.OrderBy(u => u.Price).ToList();
            for (var i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var stretch = units.Count == 1 ? 1m : 0.9m + 0.2m * i / (units.Count - 1);
                var income = Math.Round(u.Price / (payback * stretch), 4);
                sql.Append(CultureInfo.InvariantCulture,
                    $"""INSERT INTO catalogue_assets ("BusinessCatalogueId", "Id", "Name", "Price", "UnlockAtAssetCount", "FixedIncomePerSecond", "DisplayOrder", "MtbfHours") VALUES ('{b.Id}', '{u.Id}', {Str(u.Name)}, {u.Price}, {u.Unlock}, {income}, {(i + 1) * 10}, {u.Mtbf});""")
                   .AppendLine();
            }
        }

        private static string Str(string s) => $"'{s.Replace("'", "''")}'";

        // -- Content ----------------------------------------------------------------------
        // Sector: 1 Transport, 2 RealEstate, 3 StockMarket, 4 TechStartup, 5 Hospitality, 6 Energy, 7 Services.
        // Kind: 1 Fleet, 2 Sites, 3 Equipment. Tier: 1 Quiet, 2 Busy, 3 Prime.
        // The first place kind is where the business opens: its price is the opening cost.

        private sealed record K(string Id, string Name, int Tier, decimal Price);
        private sealed record U(string Id, string Name, decimal Price, int Unlock, int Mtbf);
        private sealed record Biz(
            string Id, string Name, int Sector, int Prestige, int Kind, int Order, string Description,
            K[] Kinds, U[] Units, (int, int)? Capacity = null);

        private static readonly Biz[] Catalogue =
        [
            // ── P1 · The Hustle ─────────────────────────────────────────────────────────
            new("delivery-company", "Delivery Company", 1, 1, 1, 5, "Scooters to semi trucks on delivery shifts.",
                [new("depot", "Depot", 1, 500m), new("city-hub", "City hub", 2, 1_500m), new("airport-hub", "Airport hub", 3, 4_000m)],
                [new("honda-pcx", "Honda PCX 125", 3_500m, 0, 40), new("toyota-corolla", "Toyota Corolla", 22_000m, 2, 60),
                 new("ford-transit", "Ford Transit", 46_000m, 4, 60), new("isuzu-npr", "Isuzu NPR-HD Box Truck", 90_000m, 6, 60),
                 new("volvo-vnl", "Volvo VNL 860", 165_000m, 8, 60)]),
            new("food-cart", "Food Cart", 5, 1, 2, 10, "Street food at the park, the beach and the stadium.",
                [new("park", "Park cart", 1, 500m), new("beach", "Beach cart", 2, 1_200m), new("university", "University cart", 2, 1_200m), new("stadium", "Stadium cart", 3, 3_000m)],
                [new("griddle", "Griddle", 300m, 0, 72), new("drinks-cooler", "Drinks cooler", 450m, 1, 72), new("night-generator", "Night generator", 900m, 2, 96)]),
            new("bike-courier", "Bike Courier Service", 1, 1, 1, 20, "Parcels and food by bike.",
                [new("district-hub", "District hub", 1, 1_200m), new("downtown-hub", "Downtown hub", 2, 3_000m), new("business-district", "Business district hub", 3, 6_000m)],
                [new("city-bike", "City bike", 400m, 0, 30), new("ebike", "E-bike", 2_500m, 2, 36), new("cargo-bike", "Riese & Müller cargo bike", 6_000m, 4, 36)]),
            new("phone-repair", "Phone Repair Kiosk", 4, 1, 2, 30, "Fix screens and batteries all over town.",
                [new("mall-kiosk", "Mall kiosk", 1, 2_000m), new("station-kiosk", "Station kiosk", 2, 4_500m), new("street-shop", "High-street shop", 3, 9_000m)],
                [new("repair-bench", "Repair bench", 800m, 0, 72), new("parts-stock", "Parts stock", 1_500m, 1, 96), new("micro-soldering", "Micro-soldering station", 4_000m, 3, 72)]),
            new("car-wash", "Car Wash", 7, 1, 3, 40, "From a hand-wash bay to an automatic tunnel.",
                [new("side-street", "Side street", 1, 3_000m), new("supermarket", "Next to a supermarket", 2, 7_000m), new("highway", "Highway exit", 3, 15_000m)],
                [new("hand-bay", "Hand-wash bay", 3_000m, 0, 48), new("vacuum-station", "Vacuum station", 2_000m, 1, 72),
                 new("self-service-bay", "Self-service bay", 15_000m, 2, 48), new("washtec", "WashTec SoftCare automatic", 120_000m, 4, 60)]),
            new("penny-stocks", "Penny Stock Portfolio", 3, 1, 3, 50, "Small bets that add up.",
                [new("brokerage-account", "Brokerage account", 1, 4_000m)],
                [new("small-caps", "Small-cap stocks", 1_000m, 0, 96), new("index-etf", "Index ETF", 2_500m, 1, 120),
                 new("dividend-stocks", "Dividend stocks", 6_000m, 3, 120), new("options", "Options strategy", 15_000m, 5, 48)]),
            new("parking-lot", "Parking Lot", 7, 1, 2, 60, "Lots near the places people drive to.",
                [new("hospital", "Hospital lot", 1, 5_000m), new("train-station", "Train station lot", 2, 12_000m), new("downtown", "Downtown lot", 2, 12_000m),
                 new("stadium", "Stadium lot", 3, 25_000m), new("airport", "Airport lot", 3, 30_000m)],
                [new("ticket-machine", "Ticket machine + barrier", 3_000m, 0, 48), new("cameras", "Camera system", 2_500m, 1, 96), new("ev-charger", "EV charger", 6_000m, 2, 60)]),
            new("rooftop-solar", "Rooftop Solar Kit", 6, 1, 3, 70, "Panels on roofs, power sold to the grid.",
                [new("house-roof", "House roof", 1, 8_000m), new("warehouse-roof", "Warehouse roof", 2, 18_000m)],
                [new("solar-panel", "Solar panel array", 2_000m, 0, 200), new("inverter", "Inverter", 3_500m, 1, 120), new("home-battery", "Battery storage", 9_000m, 3, 150)]),
            new("vending", "Vending Machines", 7, 1, 3, 80, "Machines where people wait.",
                [new("office", "Office", 1, 12_000m), new("school", "School", 1, 12_000m), new("hospital", "Hospital", 2, 25_000m),
                 new("train-station", "Train station", 2, 25_000m), new("airport", "Airport gates", 3, 55_000m)],
                [new("snack-machine", "Snack machine", 3_500m, 0, 36), new("drinks-machine", "Cold drinks machine", 4_500m, 1, 36), new("coffee-machine", "Coffee machine", 6_000m, 3, 30)]),

            // ── P2 · Small Business ─────────────────────────────────────────────────────
            new("mini-market", "Mini-Market", 5, 2, 2, 10, "Neighbourhood shops; open more, open longer.",
                [new("corner-shop", "Corner shop", 1, 15_000m), new("supermarket", "Supermarket", 2, 40_000m), new("store-24-7", "24/7 store", 3, 80_000m)],
                [new("drinks-fridge", "Drinks fridge", 4_000m, 0, 60), new("freezer", "Freezer", 6_000m, 1, 60),
                 new("checkout", "Checkout", 8_000m, 2, 72), new("bakery-corner", "Bakery corner", 20_000m, 4, 48)]),
            new("rent-a-car", "Car Rental", 1, 2, 1, 15, "Buy cars, rent them out by the day.",
                [new("city-centre", "City-centre agency", 1, 20_000m), new("train-station", "Station agency", 2, 50_000m), new("airport", "Airport agency", 3, 120_000m)],
                [new("dacia-sandero", "Dacia Sandero", 17_000m, 0, 48), new("toyota-corolla", "Toyota Corolla", 23_000m, 0, 60),
                 new("vw-golf", "Volkswagen Golf", 28_000m, 2, 60), new("tesla-model-3", "Tesla Model 3", 42_000m, 3, 60),
                 new("bmw-3-series", "BMW 3 Series", 46_000m, 4, 60), new("mercedes-e-class", "Mercedes-Benz E-Class", 63_000m, 6, 60),
                 new("range-rover-sport", "Range Rover Sport", 85_000m, 8, 48), new("porsche-911", "Porsche 911 Carrera", 120_000m, 10, 48)]),
            new("laundromat", "Laundromat", 7, 2, 3, 20, "Every machine earns — and can break on its own.",
                [new("apartment-blocks", "Apartment blocks", 1, 20_000m), new("student-area", "Student area", 2, 45_000m)],
                [new("speed-queen-washer", "Speed Queen washer", 1_500m, 0, 40), new("speed-queen-dryer", "Speed Queen dryer", 1_800m, 1, 48),
                 new("industrial-washer", "Electrolux industrial washer", 9_000m, 4, 60), new("dry-cleaning", "Dry-cleaning machine", 25_000m, 6, 60)]),
            new("coffee-shop", "Coffee Shop", 5, 2, 2, 30, "From a kiosk to a roastery café.",
                [new("kiosk", "Coffee kiosk", 1, 25_000m), new("cafe", "Café", 2, 60_000m), new("roastery", "Roastery café", 3, 130_000m)],
                [new("espresso-machine", "La Marzocco espresso machine", 12_000m, 0, 48), new("pastry-counter", "Pastry counter", 6_000m, 1, 72), new("terrace", "Terrace", 15_000m, 2, 120)]),
            new("web-agency", "Web Agency", 4, 2, 3, 40, "Websites and apps for local companies.",
                [new("studio", "Studio", 1, 30_000m), new("city-office", "City office", 2, 70_000m)],
                [new("developer-desk", "Developer desk", 8_000m, 0, 96), new("design-studio", "Design studio", 15_000m, 2, 96), new("hosting-server", "Hosting server", 20_000m, 3, 72)]),
            new("trading-desk", "Day-Trading Desk", 3, 2, 3, 50, "Screens, data and algorithms.",
                [new("home-office", "Home office", 1, 40_000m), new("trading-room", "Trading room", 2, 90_000m)],
                [new("trading-screen", "Trading screens", 10_000m, 0, 96), new("market-data", "Market data feed", 20_000m, 1, 120), new("trading-bot", "Trading algorithm", 30_000m, 2, 72)]),
            new("taxi-fleet", "Taxi Fleet", 1, 2, 1, 60, "Real cars, drivers day and night.",
                [new("depot", "Depot", 1, 50_000m), new("city-rank", "City-centre rank", 2, 110_000m), new("airport-permit", "Airport permit", 3, 250_000m)],
                [new("toyota-prius", "Toyota Prius", 28_000m, 0, 48), new("toyota-camry", "Toyota Camry Hybrid", 30_000m, 1, 48),
                 new("tesla-model-y", "Tesla Model Y", 45_000m, 3, 60), new("mercedes-e-class", "Mercedes-Benz E-Class", 63_000m, 5, 60)]),
            new("gym", "Gym", 7, 2, 2, 70, "Clubs whose members pay every month.",
                [new("suburb-club", "Suburb club", 1, 60_000m), new("city-club", "City club", 2, 140_000m), new("premium-club", "Premium club", 3, 300_000m)],
                [new("exercise-bike", "Life Fitness bike", 4_000m, 0, 60), new("weight-rack", "Weight rack", 3_000m, 0, 120),
                 new("treadmill", "Technogym treadmill", 9_000m, 1, 48), new("sauna", "Sauna", 25_000m, 3, 96)]),
            new("fuel-station", "Fuel Station", 6, 2, 2, 80, "Fuel stations with a shop, a car wash and EV chargers.",
                [new("neighbourhood", "Neighbourhood station", 1, 70_000m), new("town", "Town station & shop", 2, 160_000m), new("highway", "Highway service area", 3, 350_000m)],
                [new("fuel-pump", "Fuel pump", 12_000m, 0, 48), new("wash-bay", "Car-wash bay", 30_000m, 2, 60),
                 new("ev-fast-charger", "EV fast charger", 40_000m, 2, 60), new("cafe", "Café", 25_000m, 3, 72), new("convenience-store", "Convenience store", 60_000m, 3, 72)]),

            // ── P3 · Entrepreneur ───────────────────────────────────────────────────────
            new("transport-company", "Transport Company", 1, 3, 1, 10, "Vans and trucks on contracts.",
                [new("depot", "Depot", 1, 100_000m), new("city-depot", "City depot", 2, 250_000m), new("port-depot", "Port depot", 3, 600_000m)],
                [new("ford-transit", "Ford Transit", 46_000m, 0, 40), new("isuzu-npr", "Isuzu NPR-HD Box Truck", 90_000m, 2, 40),
                 new("mercedes-actros", "Mercedes-Benz Actros", 150_000m, 4, 48), new("volvo-vnl", "Volvo VNL 860", 165_000m, 6, 48)]),
            new("restaurant-chain", "Restaurant Chain", 5, 3, 2, 20, "Restaurants across town. Level each one up.",
                [new("burger-joint", "Burger Joint", 1, 150_000m), new("pizzeria", "Pizzeria", 1, 260_000m), new("sushi-bar", "Sushi Bar", 2, 380_000m), new("steakhouse", "Steakhouse", 3, 550_000m)],
                [new("fryer-station", "Fryer station", 8_000m, 0, 36), new("pizza-oven", "Wood-fired pizza oven", 18_000m, 0, 48),
                 new("rational-oven", "Rational combi oven", 25_000m, 1, 48), new("cold-room", "Cold room", 30_000m, 2, 60), new("terrace", "Terrace (20 seats)", 40_000m, 3, 120)]),
            new("bus-line", "Bus Line", 1, 3, 1, 30, "City buses and coaches on scheduled lines.",
                [new("depot", "Bus depot", 1, 180_000m), new("city-depot", "City depot", 2, 400_000m), new("intercity-terminal", "Intercity terminal", 3, 900_000m)],
                [new("minibus", "Mercedes Sprinter minibus", 70_000m, 0, 48), new("city-bus", "MAN Lion's City bus", 300_000m, 2, 48),
                 new("articulated-bus", "Mercedes Citaro G", 450_000m, 4, 48), new("coach", "Setra TopClass coach", 550_000m, 6, 60)]),
            new("small-hotel", "Small Hotel", 2, 3, 2, 40, "Rooms are the units: each earns, each can be out of order.",
                [new("city", "City hotel", 1, 200_000m), new("mountains", "Mountain hotel", 2, 450_000m), new("beach", "Beach hotel", 3, 900_000m)],
                [new("room-std", "Standard room", 5_000m, 0, 72), new("room-suite", "Suite", 15_000m, 4, 72), new("pool", "Pool", 90_000m, 8, 96), new("spa", "Spa", 120_000m, 12, 96)],
                (10, 10)),
            new("software-startup", "Software Startup", 4, 3, 3, 50, "A product, a team and the cloud.",
                [new("garage", "Garage office", 1, 250_000m), new("tech-park", "Tech park", 2, 600_000m)],
                [new("dev-team", "Dev team", 60_000m, 0, 96), new("cloud-servers", "Cloud servers", 90_000m, 1, 72), new("sales-team", "Sales team", 120_000m, 3, 96)]),
            new("investment-fund", "Investment Fund", 3, 3, 3, 60, "Funds that grow while you sleep.",
                [new("fund-office", "Fund office", 1, 300_000m)],
                [new("bond-fund", "Bond fund", 80_000m, 0, 150), new("equity-fund", "Equity fund", 150_000m, 1, 96), new("real-estate-fund", "Real-estate fund", 250_000m, 3, 120)]),
            new("private-clinic", "Private Clinic", 7, 3, 2, 70, "Dental, day and imaging clinics.",
                [new("dental-clinic", "Dental clinic", 1, 350_000m), new("day-clinic", "Day clinic", 2, 800_000m), new("imaging-centre", "Imaging centre", 3, 1_500_000m)],
                [new("dental-chair", "Dental chair", 40_000m, 0, 72), new("operating-room", "Operating room", 200_000m, 2, 72), new("mri-scanner", "MRI scanner", 300_000m, 3, 60)]),
            new("wind-farm", "Wind Farm", 6, 3, 3, 80, "Turbines on hills and at sea.",
                [new("onshore-site", "Onshore site", 1, 400_000m), new("offshore-site", "Offshore site", 3, 1_200_000m)],
                [new("vestas-v90", "Vestas V90 turbine", 150_000m, 0, 96), new("substation", "Substation", 200_000m, 1, 150), new("siemens-gamesa", "Siemens Gamesa turbine", 350_000m, 2, 96)]),

            // ── P4 · Business Mogul ─────────────────────────────────────────────────────
            new("real-estate", "Real Estate Development", 2, 4, 2, 10, "Buildings of apartments; tenants pay rent.",
                [new("cheap-district", "Affordable district", 1, 1_000_000m), new("mid-district", "Central district", 2, 2_500_000m), new("prime-district", "Prime district", 3, 6_000_000m)],
                [new("solar-panels", "Solar panels", 150_000m, 0, 200), new("apartment", "Apartment", 250_000m, 0, 72),
                 new("building", "Commercial building", 500_000m, 3, 96), new("apartment-complex", "Apartment complex", 1_500_000m, 5, 96)],
                (4, 2)),
            new("car-dealership", "Car Dealership", 7, 4, 2, 20, "From a used-car lot to a luxury showroom.",
                [new("used-car-lot", "Used-car lot", 1, 2_000_000m), new("brand-dealership", "Brand dealership", 2, 5_000_000m), new("luxury-showroom", "Luxury showroom", 3, 12_000_000m)],
                [new("service-bay", "Service bay", 300_000m, 0, 72), new("finance-desk", "Car finance desk", 400_000m, 1, 120), new("showroom-floor", "Showroom floor", 600_000m, 2, 120)]),
            new("shopping-mall", "Shopping Mall", 2, 4, 2, 30, "Let shops to brands; fill the mall to fill the bank.",
                [new("suburban", "Suburban mall", 1, 3_000_000m), new("city", "City mall", 2, 7_000_000m), new("flagship", "Flagship mall", 3, 15_000_000m)],
                [new("retail-unit", "Shop unit", 400_000m, 0, 96), new("parking-deck", "Parking deck", 600_000m, 2, 120),
                 new("food-court", "Food court", 1_200_000m, 4, 72), new("cinema", "Multiplex cinema", 1_500_000m, 6, 96)],
                (6, 4)),
            new("stock-brokerage", "Stock Brokerage", 3, 4, 3, 40, "Clients trade, you take the fees.",
                [new("brokerage-office", "Brokerage office", 1, 4_000_000m), new("wall-street-office", "Wall Street office", 3, 10_000_000m)],
                [new("research-desk", "Research desk", 800_000m, 0, 120), new("trading-floor", "Trading floor", 1_000_000m, 1, 96), new("retail-app", "Retail trading app", 1_500_000m, 2, 72)]),
            new("tech-unicorn", "Tech Unicorn", 4, 4, 3, 50, "A billion-dollar app in the making.",
                [new("campus", "Campus", 1, 5_000_000m), new("global-campus", "Global campus", 3, 12_000_000m)],
                [new("marketing-team", "Marketing team", 1_000_000m, 0, 120), new("product-team", "Product team", 1_500_000m, 1, 96), new("data-centre", "Data centre", 3_000_000m, 2, 72)]),
            new("solar-park", "Solar Park", 6, 4, 3, 60, "Fields of panels in the sun.",
                [new("desert-site", "Desert site", 1, 6_000_000m), new("coastal-site", "Coastal site", 2, 14_000_000m)],
                [new("solar-trackers", "Solar trackers", 1_000_000m, 0, 150), new("panel-field", "Panel field", 1_500_000m, 0, 200), new("battery-storage", "Battery storage", 2_500_000m, 2, 150)]),
            new("logistics-hub", "Logistics Hub", 1, 4, 1, 70, "Semi trucks and robot warehouses.",
                [new("regional-hub", "Regional hub", 1, 7_000_000m), new("port-hub", "Port hub", 3, 18_000_000m)],
                [new("cargo-van", "Mercedes eSprinter", 70_000m, 0, 60), new("volvo-fh", "Volvo FH semi truck", 160_000m, 0, 48),
                 new("scania-r", "Scania R semi truck", 180_000m, 1, 48), new("warehouse-robots", "Warehouse robots", 500_000m, 3, 72)],
                (6, 3)),
            new("luxury-resort", "Luxury Resort", 5, 4, 2, 80, "Island, ski and desert resorts.",
                [new("island", "Island resort", 1, 8_000_000m), new("ski", "Ski resort", 2, 18_000_000m), new("desert", "Desert resort", 3, 40_000_000m)],
                [new("villa", "Private villa", 1_000_000m, 0, 96), new("spa", "Spa", 2_000_000m, 2, 96), new("yacht-club", "Yacht club", 3_000_000m, 4, 120)]),

            // ── P5 · Tycoon ─────────────────────────────────────────────────────────────
            new("office-tower", "Office Tower", 2, 5, 2, 10, "Floors rented to companies.",
                [new("business-park", "Business park tower", 1, 15_000_000m), new("cbd", "Central business district tower", 2, 35_000_000m), new("landmark", "Landmark tower", 3, 80_000_000m)],
                [new("floor", "Office floor", 1_000_000m, 0, 120), new("conference-center", "Conference center", 4_000_000m, 3, 96), new("rooftop-helipad", "Rooftop helipad", 10_000_000m, 6, 120)],
                (10, 5)),
            new("cargo-airline", "Cargo Airline", 1, 5, 1, 20, "Freighters flying parcels across the world.",
                [new("regional-base", "Regional base", 1, 30_000_000m), new("hub-base", "Hub base", 2, 70_000_000m), new("global-hub", "Global hub", 3, 160_000_000m)],
                [new("atr-72f", "ATR 72-600F", 26_000_000m, 0, 72), new("b737-bcf", "Boeing 737-800BCF", 50_000_000m, 1, 72),
                 new("b767f", "Boeing 767-300F", 120_000_000m, 3, 72), new("b777f", "Boeing 777F", 350_000_000m, 5, 72)]),
            new("streaming-platform", "Streaming Platform", 4, 5, 3, 30, "Series, films and subscribers.",
                [new("studio", "Studio", 1, 40_000_000m), new("global-studio", "Global studio", 3, 100_000_000m)],
                [new("regional-launch", "Regional launch", 8_000_000m, 0, 120), new("original-series", "Original series", 10_000_000m, 1, 96), new("data-centre", "Data centre", 15_000_000m, 2, 72)]),
            new("consulting-firm", "Global Consulting Firm", 7, 5, 2, 40, "Offices in the world's business cities.",
                [new("london-office", "London office", 1, 50_000_000m), new("new-york-office", "New York office", 2, 110_000_000m), new("singapore-office", "Singapore office", 3, 250_000_000m)],
                [new("partner-team", "Partner team", 8_000_000m, 0, 120), new("analytics-lab", "Analytics lab", 12_000_000m, 2, 96)]),
            new("investment-bank", "Investment Bank", 3, 5, 3, 50, "Mergers, trading and private banking.",
                [new("headquarters", "Headquarters", 1, 60_000_000m)],
                [new("ma-desk", "M&A desk", 15_000_000m, 0, 96), new("private-banking", "Private banking", 20_000_000m, 1, 120), new("trading-floor", "Trading floor", 25_000_000m, 2, 72)]),
            new("cruise-line", "Cruise Line", 5, 5, 1, 60, "River cruisers to mega ships.",
                [new("home-port", "Home port", 1, 90_000_000m), new("caribbean-port", "Caribbean port", 2, 200_000_000m), new("mediterranean-port", "Mediterranean port", 3, 450_000_000m)],
                [new("river-cruiser", "River cruiser", 60_000_000m, 0, 96), new("mid-size-ship", "Mid-size cruise ship", 300_000_000m, 2, 96), new("mega-ship", "Mega cruise ship", 900_000_000m, 4, 96)]),
            new("nuclear-plant", "Nuclear Power Plant", 6, 5, 3, 70, "Reactors that power whole regions.",
                [new("plant-site", "Plant site", 1, 120_000_000m)],
                [new("cooling-tower", "Cooling tower", 80_000_000m, 0, 200), new("turbine-hall", "Turbine hall", 150_000_000m, 0, 150), new("reactor", "Reactor unit", 500_000_000m, 2, 200)]),
            new("airport", "International Airport", 1, 5, 2, 80, "Gates, airlines and everything around them.",
                [new("regional", "Regional airport", 1, 200_000_000m), new("hub", "International hub", 2, 450_000_000m)],
                [new("lounge", "Lounge", 8_000_000m, 0, 120), new("gate", "Gate", 10_000_000m, 0, 96), new("duty-free", "Duty-free shop", 20_000_000m, 2, 120), new("terminal", "Terminal", 50_000_000m, 4, 150)],
                (4, 2)),

            // ── P6 · Billionaire ────────────────────────────────────────────────────────
            new("skyscraper-district", "Skyscraper District", 2, 6, 2, 10, "Skyscrapers in the world's capitals.",
                [new("dubai", "Dubai district", 1, 400_000_000m), new("shanghai", "Shanghai district", 2, 900_000_000m), new("manhattan", "Manhattan district", 3, 2_000_000_000m)],
                [new("sky-floors", "Office floors", 60_000_000m, 0, 120), new("observation-deck", "Observation deck", 100_000_000m, 2, 120), new("sky-hotel", "Sky hotel", 200_000_000m, 3, 96)]),
            new("space-tourism", "Space Tourism", 1, 6, 1, 20, "Tickets to the edge of space and beyond.",
                [new("spaceport", "Spaceport", 1, 500_000_000m), new("equatorial-spaceport", "Equatorial spaceport", 3, 1_200_000_000m)],
                [new("suborbital", "Suborbital spaceplane", 250_000_000m, 0, 72), new("orbital-capsule", "Orbital capsule", 600_000_000m, 2, 72), new("space-hotel", "Space hotel module", 1_500_000_000m, 4, 96)]),
            new("global-bank", "Global Bank", 3, 6, 2, 30, "Branches in every financial centre.",
                [new("frankfurt-branch", "Frankfurt branch", 1, 800_000_000m), new("london-branch", "London branch", 2, 1_800_000_000m), new("new-york-branch", "New York branch", 3, 4_000_000_000m)],
                [new("retail-network", "Retail branches", 150_000_000m, 0, 120), new("wealth-desk", "Wealth management", 250_000_000m, 1, 120), new("trading-floor", "Trading floor", 400_000_000m, 3, 72)]),
            new("hotel-empire", "Hotel Empire", 5, 6, 2, 40, "Business, resort and palace hotels.",
                [new("business-hotel", "Business hotel", 1, 900_000_000m), new("resort-hotel", "Resort hotel", 2, 2_000_000_000m), new("palace-hotel", "Palace hotel", 3, 4_500_000_000m)],
                [new("ballroom", "Ballroom", 100_000_000m, 0, 120), new("spa", "Spa", 150_000_000m, 1, 96), new("michelin-restaurant", "Michelin restaurant", 250_000_000m, 3, 96)]),
            new("ai-superlab", "AI Superlab", 4, 6, 3, 50, "GPU clusters and the people who use them.",
                [new("lab", "Lab", 1, 1_000_000_000m), new("mega-lab", "Mega lab", 3, 2_500_000_000m)],
                [new("research-team", "Research team", 200_000_000m, 0, 120), new("gpu-cluster", "GPU cluster", 300_000_000m, 1, 72), new("inference-farm", "Inference farm", 500_000_000m, 3, 72)]),
            new("airline-group", "Airline Group", 1, 6, 1, 60, "Passenger jets on routes around the world.",
                [new("home-hub", "Home hub", 1, 1_200_000_000m), new("second-hub", "Second hub", 2, 2_600_000_000m), new("global-alliance", "Global alliance hub", 3, 6_000_000_000m)],
                [new("a320neo", "Airbus A320neo", 110_000_000m, 0, 72), new("b787", "Boeing 787-9 Dreamliner", 290_000_000m, 2, 72),
                 new("a350", "Airbus A350-1000", 370_000_000m, 4, 72), new("a380", "Airbus A380", 450_000_000m, 6, 72)]),
            new("oil-major", "Oil Major", 6, 6, 3, 70, "Rigs, refineries and tankers.",
                [new("north-sea", "North Sea field", 1, 1_500_000_000m), new("gulf", "Gulf field", 3, 3_500_000_000m)],
                [new("oil-tanker", "Oil tanker", 150_000_000m, 0, 72), new("oil-rig", "Offshore oil rig", 600_000_000m, 1, 96), new("refinery", "Refinery", 900_000_000m, 2, 150)]),
            new("container-shipping", "Container Shipping", 1, 6, 1, 80, "Container ships between the world's ports.",
                [new("rotterdam", "Rotterdam terminal", 1, 2_000_000_000m), new("singapore", "Singapore terminal", 2, 4_500_000_000m), new("shanghai", "Shanghai terminal", 3, 10_000_000_000m)],
                [new("feeder-ship", "Feeder ship", 30_000_000m, 0, 96), new("panamax", "Panamax container ship", 90_000_000m, 2, 96), new("ulcv", "Ultra-large container ship", 200_000_000m, 4, 96)],
                (6, 3)),

            // ── P7 · Global Empire ──────────────────────────────────────────────────────
            new("global-consultancy", "World Advisory Group", 7, 7, 2, 10, "Regional headquarters advising governments.",
                [new("americas-hq", "Americas headquarters", 1, 6_000_000_000m), new("emea-hq", "EMEA headquarters", 2, 13_000_000_000m), new("apac-hq", "Asia-Pacific headquarters", 3, 28_000_000_000m)],
                [new("ai-practice", "AI practice", 1_000_000_000m, 0, 120), new("strategy-team", "Strategy team", 1_500_000_000m, 1, 120)]),
            new("world-exchange", "World Stock Exchange", 3, 7, 3, 20, "Where the world's companies list.",
                [new("exchange-floor", "Exchange floor", 1, 8_000_000_000m)],
                [new("data-feed", "Market data network", 1_500_000_000m, 0, 120), new("listing-venue", "Listing venue", 2_000_000_000m, 1, 120), new("clearing-house", "Clearing house", 3_000_000_000m, 2, 150)]),
            new("global-resorts", "Planet Resorts", 5, 7, 2, 30, "Resort islands on every continent.",
                [new("caribbean", "Caribbean resort", 1, 9_000_000_000m), new("maldives", "Maldives resort", 2, 20_000_000_000m), new("antarctica", "Antarctica resort", 3, 45_000_000_000m)],
                [new("island-villas", "Private island villas", 1_500_000_000m, 0, 96), new("underwater-suite", "Underwater suite", 2_500_000_000m, 2, 96)]),
            new("mars-colony", "Mars Colony", 1, 7, 1, 40, "Starships, habitats and rovers on Mars.",
                [new("launch-site", "Launch site", 1, 10_000_000_000m), new("mars-base", "Mars base", 3, 25_000_000_000m)],
                [new("rover", "Pressurized rover", 800_000_000m, 0, 96), new("starship", "Starship", 2_000_000_000m, 1, 72), new("habitat", "Mars habitat", 3_000_000_000m, 2, 120)]),
            new("metaverse", "Metaverse Platform", 4, 7, 3, 50, "Virtual worlds with real money.",
                [new("platform", "Platform", 1, 12_000_000_000m)],
                [new("creator-fund", "Creator fund", 2_000_000_000m, 0, 120), new("virtual-world", "Virtual world", 3_000_000_000m, 1, 96), new("server-farm", "Server farm", 4_000_000_000m, 2, 72)]),
            new("hyperloop", "Hyperloop Network", 1, 7, 1, 60, "Pods at airline speed between cities.",
                [new("coast-line", "Coast-to-coast line", 1, 14_000_000_000m), new("transcontinental-line", "Transcontinental line", 3, 35_000_000_000m)],
                [new("passenger-pod", "Passenger pod", 500_000_000m, 0, 72), new("cargo-pod", "Cargo pod", 800_000_000m, 1, 72), new("station", "Station", 2_000_000_000m, 3, 150)]),
            new("fusion-grid", "Global Fusion Grid", 6, 7, 3, 70, "Clean power for the whole planet.",
                [new("grid", "Grid", 1, 15_000_000_000m)],
                [new("grid-line", "Superconducting line", 2_000_000_000m, 0, 200), new("grid-storage", "Grid storage", 3_000_000_000m, 1, 150), new("fusion-reactor", "Fusion reactor", 6_000_000_000m, 2, 150)]),
            new("megacity", "Megacity", 2, 7, 2, 80, "Districts you build and level up.",
                [new("new-district", "New district", 1, 20_000_000_000m), new("waterfront-district", "Waterfront district", 2, 45_000_000_000m), new("sky-district", "Sky district", 3, 100_000_000_000m)],
                [new("smart-grid", "Smart grid", 3_000_000_000m, 0, 150), new("tower-block", "Residential towers", 4_000_000_000m, 1, 120), new("transit-hub", "Transit hub", 6_000_000_000m, 2, 120)]),
        ];
    }
}
