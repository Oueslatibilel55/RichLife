using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <summary>
    /// Places, units and breakdowns (features/014-business-places-and-equipment.md).
    /// Schema, then content (place kinds and real units), then every owned business
    /// gets its first place holding what it already owns — its income is unchanged — and
    /// last the old <c>GrossIncomePerSecond</c> column goes.
    /// </summary>
    public partial class BusinessPlacesAndUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -- 1. Schema ----------------------------------------------------------------

            migrationBuilder.AddColumn<int>(
                name: "PlaceBaseCapacity", table: "catalogue_businesses",
                type: "integer", nullable: false, defaultValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "PlaceCapacityPerLevel", table: "catalogue_businesses",
                type: "integer", nullable: false, defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "MtbfHours", table: "catalogue_assets",
                type: "integer", nullable: false, defaultValue: 48);

            migrationBuilder.CreateTable(
                name: "catalogue_place_kinds",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    BusinessCatalogueId = table.Column<string>(type: "character varying(60)", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogue_place_kinds", x => new { x.BusinessCatalogueId, x.Id });
                    table.ForeignKey(
                        name: "FK_catalogue_place_kinds_catalogue_businesses_BusinessCatalogu~",
                        column: x => x.BusinessCatalogueId,
                        principalTable: "catalogue_businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "business_places",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    KindId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Street = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    BaseIncomePerSecond = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    BaseCapacity = table.Column<int>(type: "integer", nullable: false),
                    CapacityPerLevel = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_places", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_places_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_places_BusinessId", table: "business_places", column: "BusinessId");

            migrationBuilder.AddColumn<string>(
                name: "CatalogueId", table: "BusinessAssets",
                type: "character varying(60)", maxLength: 60, nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MtbfHours", table: "BusinessAssets",
                type: "integer", nullable: false, defaultValue: 48);

            migrationBuilder.AddColumn<Guid>(
                name: "PlaceId", table: "BusinessAssets", type: "uuid", nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepairCount", table: "BusinessAssets",
                type: "integer", nullable: false, defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "WorkingFrom", table: "BusinessAssets",
                type: "timestamp with time zone", nullable: false, defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "BreaksAt", table: "BusinessAssets",
                type: "timestamp with time zone", nullable: false, defaultValueSql: "now()");

            // -- 2. Content ---------------------------------------------------------------

            var sql = new StringBuilder();
            foreach (var (business, baseCapacity, perLevel) in Capacities)
                sql.Append(CultureInfo.InvariantCulture,
                    $"""UPDATE catalogue_businesses SET "PlaceBaseCapacity" = {baseCapacity}, "PlaceCapacityPerLevel" = {perLevel} WHERE "Id" = '{business}';""")
                   .AppendLine();

            // The original generic assets give way to real units. Only the seeded rows go —
            // anything an admin added stays. Owned units are copies and are not touched.
            foreach (var (business, ids) in RetiredSeedAssets)
                sql.Append(CultureInfo.InvariantCulture,
                    $"""DELETE FROM catalogue_assets WHERE "BusinessCatalogueId" = '{business}' AND "Id" IN ({string.Join(", ", ids.Select(i => $"'{i}'"))});""")
                   .AppendLine();

            foreach (var k in PlaceKinds)
                sql.Append(CultureInfo.InvariantCulture,
                    $"""INSERT INTO catalogue_place_kinds ("BusinessCatalogueId", "Id", "Name", "Tier", "Price", "DisplayOrder") SELECT '{k.Business}', '{k.Id}', {Str(k.Name)}, {k.Tier}, {k.Price}, {k.Order} WHERE EXISTS (SELECT 1 FROM catalogue_businesses WHERE "Id" = '{k.Business}') ON CONFLICT DO NOTHING;""")
                   .AppendLine();

            foreach (var a in Units)
                sql.Append(CultureInfo.InvariantCulture,
                    $"""INSERT INTO catalogue_assets ("BusinessCatalogueId", "Id", "Name", "Price", "UnlockAtAssetCount", "FixedIncomePerSecond", "DisplayOrder", "MtbfHours") SELECT '{a.Business}', '{a.Id}', {Str(a.Name)}, {a.Price}, {a.Unlock}, {(a.Income is { } i ? i.ToString(CultureInfo.InvariantCulture) : "NULL")}, {a.Order}, {a.Mtbf} WHERE EXISTS (SELECT 1 FROM catalogue_businesses WHERE "Id" = '{a.Business}') ON CONFLICT DO NOTHING;""")
                   .AppendLine();

            migrationBuilder.Sql(sql.ToString());

            // -- 3. Every owned business gets its first place ---------------------------
            // The first place kind (all quiet, × 1), level 1, holding every unit it owns, earning
            // what the business earned on its own (gross minus its assets) — so income is
            // unchanged. Capacity is at least what it already holds.
            migrationBuilder.Sql("""
                INSERT INTO business_places
                    ("Id", "BusinessId", "KindId", "Name", "Street", "Tier", "Level", "BaseIncomePerSecond",
                     "BaseCapacity", "CapacityPerLevel", "Price", "PurchasePrice", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), b."Id",
                       COALESCE(k."Id", 'main'), COALESCE(k."Name", 'Main'),
                       (1 + floor(random() * 180))::int || ' ' ||
                           (ARRAY['Oak Street','Maple Avenue','Harbour Road','Station Square','Market Street',
                                  'Elm Grove','Riverside Drive','Park Lane','Mill Road','High Street'])[1 + floor(random() * 10)::int],
                       COALESCE(k."Tier", 1), 1,
                       GREATEST(b."GrossIncomePerSecond"
                                - COALESCE((SELECT SUM(a."IncomePerSecond") FROM "BusinessAssets" a WHERE a."BusinessId" = b."Id"), 0), 0),
                       GREATEST(c."PlaceBaseCapacity",
                                (SELECT COUNT(*) FROM "BusinessAssets" a WHERE a."BusinessId" = b."Id")::int),
                       c."PlaceCapacityPerLevel",
                       COALESCE(k."Price", b."OpeningCost"), 0,
                       b."CreatedAt", now()
                FROM businesses b
                JOIN catalogue_businesses c ON c."Id" = b."CatalogueId"
                LEFT JOIN LATERAL (
                    SELECT pk."Id", pk."Name", pk."Tier", pk."Price"
                    FROM catalogue_place_kinds pk
                    WHERE pk."BusinessCatalogueId" = c."Id"
                    ORDER BY pk."DisplayOrder", pk."Id"
                    LIMIT 1) k ON true;
                """);

            // Units: into that place; catalogue id and MTBF by name where the catalogue still
            // has it; first failure 0.5–1.5 × MTBF from now, so they do not all break at once.
            migrationBuilder.Sql("""
                UPDATE "BusinessAssets" a
                SET "PlaceId" = p."Id"
                FROM business_places p
                WHERE p."BusinessId" = a."BusinessId";

                UPDATE "BusinessAssets" a
                SET "CatalogueId" = ca."Id", "MtbfHours" = ca."MtbfHours"
                FROM businesses b
                JOIN catalogue_assets ca ON ca."BusinessCatalogueId" = b."CatalogueId"
                WHERE b."Id" = a."BusinessId" AND ca."Name" = a."Name";

                UPDATE "BusinessAssets"
                SET "WorkingFrom" = now(),
                    "BreaksAt" = now() + make_interval(secs => "MtbfHours" * 3600 * (0.5 + random()));
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "PlaceId", table: "BusinessAssets", type: "uuid", nullable: false,
                oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAssets_PlaceId", table: "BusinessAssets", column: "PlaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessAssets_business_places_PlaceId",
                table: "BusinessAssets",
                column: "PlaceId",
                principalTable: "business_places",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // -- 4. Income now comes from places and units ------------------------------
            migrationBuilder.DropColumn(name: "GrossIncomePerSecond", table: "businesses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GrossIncomePerSecond", table: "businesses",
                type: "numeric(20,6)", precision: 20, scale: 6, nullable: false, defaultValue: 0m);

            // Back to one number per business: every place's own income plus every unit's.
            migrationBuilder.Sql("""
                UPDATE businesses b SET "GrossIncomePerSecond" =
                    COALESCE((SELECT SUM(p."BaseIncomePerSecond") FROM business_places p WHERE p."BusinessId" = b."Id"), 0)
                  + COALESCE((SELECT SUM(a."IncomePerSecond") FROM "BusinessAssets" a WHERE a."BusinessId" = b."Id"), 0);
                """);

            migrationBuilder.DropForeignKey(name: "FK_BusinessAssets_business_places_PlaceId", table: "BusinessAssets");
            migrationBuilder.DropIndex(name: "IX_BusinessAssets_PlaceId", table: "BusinessAssets");
            migrationBuilder.DropTable(name: "business_places");
            migrationBuilder.DropTable(name: "catalogue_place_kinds");

            migrationBuilder.DropColumn(name: "PlaceBaseCapacity", table: "catalogue_businesses");
            migrationBuilder.DropColumn(name: "PlaceCapacityPerLevel", table: "catalogue_businesses");
            migrationBuilder.DropColumn(name: "MtbfHours", table: "catalogue_assets");
            migrationBuilder.DropColumn(name: "BreaksAt", table: "BusinessAssets");
            migrationBuilder.DropColumn(name: "CatalogueId", table: "BusinessAssets");
            migrationBuilder.DropColumn(name: "MtbfHours", table: "BusinessAssets");
            migrationBuilder.DropColumn(name: "PlaceId", table: "BusinessAssets");
            migrationBuilder.DropColumn(name: "RepairCount", table: "BusinessAssets");
            migrationBuilder.DropColumn(name: "WorkingFrom", table: "BusinessAssets");
            // The new catalogue units and place kinds stay: they are content.
        }

        private static string Str(string s) => $"'{s.Replace("'", "''")}'";

        // -- Content ------------------------------------------------------------------------

        private static readonly (string Business, int Base, int PerLevel)[] Capacities =
        [
            ("food-cart", 1, 1), ("bike-courier", 2, 2), ("car-wash", 2, 1), ("parking-lot", 2, 2),
            ("vending", 3, 2), ("mini-market", 3, 2), ("laundromat", 4, 2), ("taxi-fleet", 2, 2),
            ("gym", 4, 3), ("transport-company", 3, 2), ("rent-a-car", 4, 3), ("restaurant-chain", 3, 1),
            ("small-hotel", 10, 10), ("fuel-station", 4, 2), ("real-estate", 4, 2), ("shopping-mall", 6, 4),
            ("office-tower", 10, 5), ("airport", 4, 2),
        ];

        private static readonly (string Business, string[] Ids)[] RetiredSeedAssets =
        [
            ("food-cart", ["menu-item", "menu-item-2"]),
            ("bike-courier", ["bike", "ebike"]),
            ("car-wash", ["wash-kit", "auto-wash"]),
            ("parking-lot", ["signage", "cameras"]),
            ("vending", ["machine", "machine-xl"]),
            ("mini-market", ["stock-basic", "stock-premium"]),
            ("laundromat", ["machine-std", "machine-pro"]),
            ("taxi-fleet", ["dacia", "renault", "toyota", "bmw3"]),
            ("gym", ["equipment-basic", "equipment-pro", "pool"]),
            ("transport-company", ["fiat-fiorino", "iveco-daily", "mercedes-actros", "volvo-fh", "man-tgx", "scania-r"]),
            ("rent-a-car", ["fiat-punto", "renault-clio", "vw-passat", "bmw-3", "mercedes-e", "porsche"]),
            ("restaurant-chain", ["location"]),
            ("small-hotel", ["room-std", "room-suite"]),
            ("real-estate", ["building"]),
            ("shopping-mall", ["retail-unit"]),
            ("office-tower", ["floor"]),
            ("airport", ["terminal"]),
        ];

        // Tier: 1 Quiet (× 1), 2 Busy (× 1.5), 3 Prime (× 2.2). The first kind of each business
        // (lowest order) is quiet: it is the place the business opens with.
        private static readonly (string Business, string Id, string Name, int Tier, decimal Price, int Order)[] PlaceKinds =
        [
            ("food-cart", "park", "Park", 1, 500m, 10),
            ("food-cart", "beach", "Beach", 2, 1_000m, 20),
            ("food-cart", "university", "University", 2, 1_000m, 30),
            ("food-cart", "stadium", "Stadium", 3, 2_000m, 40),

            ("bike-courier", "district-hub", "District hub", 1, 1_200m, 10),
            ("bike-courier", "downtown-hub", "Downtown hub", 2, 2_400m, 20),
            ("bike-courier", "business-district", "Business district hub", 3, 4_800m, 30),

            ("car-wash", "side-street", "Side street", 1, 3_000m, 10),
            ("car-wash", "supermarket", "Next to a supermarket", 2, 6_000m, 20),
            ("car-wash", "highway", "Highway exit", 3, 12_000m, 30),

            ("parking-lot", "hospital", "Hospital", 1, 5_000m, 10),
            ("parking-lot", "train-station", "Train station", 2, 10_000m, 20),
            ("parking-lot", "downtown", "Downtown", 2, 10_000m, 30),
            ("parking-lot", "stadium", "Stadium", 3, 20_000m, 40),
            ("parking-lot", "airport", "Airport", 3, 20_000m, 50),

            ("vending", "office", "Office", 1, 12_000m, 10),
            ("vending", "school", "School", 1, 12_000m, 20),
            ("vending", "hospital", "Hospital", 2, 24_000m, 30),
            ("vending", "train-station", "Train station", 2, 24_000m, 40),
            ("vending", "airport", "Airport gates", 3, 48_000m, 50),

            ("mini-market", "neighbourhood", "Neighbourhood", 1, 15_000m, 10),
            ("mini-market", "near-school", "Near a school", 2, 30_000m, 20),
            ("mini-market", "near-station", "Near a station", 2, 30_000m, 30),

            ("laundromat", "apartment-blocks", "Apartment blocks", 1, 20_000m, 10),
            ("laundromat", "student-area", "Student area", 2, 40_000m, 20),

            ("taxi-fleet", "depot", "Depot", 1, 50_000m, 10),
            ("taxi-fleet", "city-rank", "City-centre rank", 2, 100_000m, 20),
            ("taxi-fleet", "airport-permit", "Airport permit", 3, 200_000m, 30),

            ("gym", "suburb-club", "Suburb club", 1, 60_000m, 10),
            ("gym", "city-club", "City club", 2, 120_000m, 20),
            ("gym", "prime-club", "Prime club", 3, 240_000m, 30),

            ("transport-company", "depot", "Depot", 1, 100_000m, 10),
            ("transport-company", "city-depot", "City depot", 2, 200_000m, 20),
            ("transport-company", "port-depot", "Port depot", 3, 400_000m, 30),

            ("rent-a-car", "city-centre", "City centre", 1, 80_000m, 10),
            ("rent-a-car", "train-station", "Train station", 2, 160_000m, 20),
            ("rent-a-car", "airport", "Airport", 3, 320_000m, 30),

            ("restaurant-chain", "side-street", "Side street", 1, 150_000m, 10),
            ("restaurant-chain", "downtown", "Downtown", 2, 300_000m, 20),
            ("restaurant-chain", "waterfront", "Waterfront", 3, 600_000m, 30),

            ("small-hotel", "city", "City", 1, 200_000m, 10),
            ("small-hotel", "mountains", "Mountains", 2, 400_000m, 20),
            ("small-hotel", "beach", "Beach", 3, 800_000m, 30),

            ("fuel-station", "neighbourhood", "Neighbourhood station", 1, 70_000m, 10),
            ("fuel-station", "town", "Town station & shop", 2, 140_000m, 20),
            ("fuel-station", "highway", "Highway service area", 3, 280_000m, 30),

            ("real-estate", "cheap-district", "Affordable district", 1, 1_000_000m, 10),
            ("real-estate", "mid-district", "Central district", 2, 2_000_000m, 20),
            ("real-estate", "prime-district", "Prime district", 3, 4_000_000m, 30),

            ("shopping-mall", "suburban", "Suburban mall", 1, 3_000_000m, 10),
            ("shopping-mall", "city", "City mall", 2, 6_000_000m, 20),
            ("shopping-mall", "flagship", "Flagship mall", 3, 12_000_000m, 30),

            ("office-tower", "business-park", "Business park", 1, 15_000_000m, 10),
            ("office-tower", "cbd", "Central business district", 2, 30_000_000m, 20),

            ("airport", "regional", "Regional site", 1, 200_000_000m, 10),
            ("airport", "hub", "International hub", 2, 400_000_000m, 20),
        ];

        // Income null = derived from price (× 0.0003/s), as before for vehicles.
        private static readonly (string Business, string Id, string Name, decimal Price, int Unlock, decimal? Income, int Order, int Mtbf)[] Units =
        [
            ("food-cart", "hot-dog-cart", "Hot-dog cart", 500m, 0, 0.5m, 10, 48),
            ("food-cart", "coffee-bike", "Coffee bike", 1_800m, 2, 1.8m, 20, 36),
            ("food-cart", "food-truck", "Ford E-450 food truck", 65_000m, 5, 55m, 30, 48),

            ("bike-courier", "city-bike", "City bike", 400m, 0, 0.4m, 10, 30),
            ("bike-courier", "ebike", "E-bike", 2_500m, 3, 2.5m, 20, 36),
            ("bike-courier", "scooter", "Honda PCX 125 scooter", 3_500m, 4, 3.5m, 30, 40),
            ("bike-courier", "cargo-bike", "Riese & Müller cargo bike", 6_000m, 5, 6m, 40, 36),

            ("car-wash", "hand-bay", "Hand-wash bay", 3_000m, 0, 3m, 10, 48),
            ("car-wash", "vacuum-station", "Vacuum station", 2_000m, 1, 2m, 20, 72),
            ("car-wash", "self-service-bay", "Self-service bay", 15_000m, 2, 13m, 30, 48),
            ("car-wash", "washtec", "WashTec SoftCare automatic", 180_000m, 4, 140m, 40, 60),

            ("parking-lot", "space-block", "Block of 20 spaces", 4_000m, 0, 4m, 10, 96),
            ("parking-lot", "ticket-machine", "Ticket machine + barrier", 6_000m, 1, 6.5m, 20, 48),
            ("parking-lot", "cameras", "Camera system", 5_000m, 2, 5m, 30, 96),
            ("parking-lot", "ev-charger", "EV charger", 9_000m, 3, 9.5m, 40, 60),

            ("vending", "snack-machine", "Snack machine", 3_500m, 0, 3.5m, 10, 36),
            ("vending", "drinks-machine", "Cold drinks machine", 4_500m, 1, 4.6m, 20, 36),
            ("vending", "coffee-machine", "Coffee machine", 6_000m, 3, 6.2m, 30, 30),

            ("mini-market", "drinks-fridge", "Drinks fridge", 4_000m, 0, 4m, 10, 60),
            ("mini-market", "freezer", "Freezer", 6_000m, 1, 6m, 20, 60),
            ("mini-market", "checkout", "Checkout", 8_000m, 2, 8.5m, 30, 72),
            ("mini-market", "bakery-corner", "Bakery corner", 20_000m, 4, 20m, 40, 48),

            ("laundromat", "speed-queen-washer", "Speed Queen washer", 1_500m, 0, 1.5m, 10, 40),
            ("laundromat", "speed-queen-dryer", "Speed Queen dryer", 1_800m, 1, 1.8m, 20, 48),
            ("laundromat", "industrial-washer", "Electrolux industrial washer", 9_000m, 4, 9.5m, 30, 60),

            ("taxi-fleet", "toyota-prius", "Toyota Prius", 28_000m, 0, null, 10, 48),
            ("taxi-fleet", "toyota-camry", "Toyota Camry Hybrid", 30_000m, 1, null, 20, 48),
            ("taxi-fleet", "tesla-model-y", "Tesla Model Y", 45_000m, 3, null, 30, 60),
            ("taxi-fleet", "mercedes-e-class", "Mercedes E-Class", 63_000m, 5, null, 40, 60),

            ("gym", "rowing-machine", "Rowing machine", 1_500m, 0, 1.5m, 10, 72),
            ("gym", "weight-rack", "Weight rack", 3_000m, 0, 3m, 20, 120),
            ("gym", "exercise-bike", "Life Fitness bike", 4_000m, 1, 4m, 30, 60),
            ("gym", "treadmill", "Technogym treadmill", 9_000m, 2, 9m, 40, 48),

            ("transport-company", "ford-transit", "Ford Transit", 46_000m, 0, null, 10, 40),
            ("transport-company", "isuzu-npr", "Isuzu NPR-HD", 90_000m, 2, null, 20, 40),
            ("transport-company", "mercedes-actros", "Mercedes-Benz Actros", 150_000m, 4, null, 30, 48),
            ("transport-company", "volvo-vnl", "Volvo VNL 860", 165_000m, 6, null, 40, 48),

            ("rent-a-car", "dacia-sandero", "Dacia Sandero", 15_000m, 0, null, 10, 48),
            ("rent-a-car", "toyota-corolla", "Toyota Corolla", 24_000m, 0, null, 20, 60),
            ("rent-a-car", "vw-golf", "Volkswagen Golf", 30_000m, 2, null, 30, 60),
            ("rent-a-car", "tesla-model-3", "Tesla Model 3", 42_000m, 3, null, 40, 60),
            ("rent-a-car", "bmw-3-series", "BMW 3 Series", 48_000m, 4, null, 50, 60),
            ("rent-a-car", "mercedes-e-class", "Mercedes E-Class", 63_000m, 6, null, 60, 60),
            ("rent-a-car", "range-rover-sport", "Range Rover Sport", 90_000m, 8, null, 70, 48),
            ("rent-a-car", "porsche-911", "Porsche 911", 125_000m, 10, null, 80, 48),

            ("restaurant-chain", "fryer-station", "Fryer station", 8_000m, 0, 7m, 10, 36),
            ("restaurant-chain", "pizza-oven", "Wood-fired pizza oven", 18_000m, 0, 16m, 20, 48),
            ("restaurant-chain", "rational-oven", "Rational combi oven", 25_000m, 1, 22m, 30, 48),
            ("restaurant-chain", "cold-room", "Cold room", 30_000m, 2, 26m, 40, 60),
            ("restaurant-chain", "terrace", "Terrace (20 seats)", 40_000m, 3, 36m, 50, 120),

            ("small-hotel", "room-std", "Standard room", 5_000m, 0, 4.5m, 10, 72),
            ("small-hotel", "room-suite", "Suite", 15_000m, 10, 13.5m, 20, 72),
            ("small-hotel", "pool", "Pool", 90_000m, 15, 75m, 30, 96),
            ("small-hotel", "spa", "Spa", 120_000m, 20, 100m, 40, 96),

            // Fuel Station (an admin-made entry, P2) is the design book's Gas Stations: it already sells a
            // pump, a car-wash bay and a shop; these complete it. Skipped where the entry does not exist.
            ("fuel-station", "ev-fast-charger", "EV fast charger", 40_000m, 2, 32m, 40, 60),
            ("fuel-station", "cafe", "Café", 25_000m, 3, 20m, 50, 72),

            ("real-estate", "solar-panels", "Solar panels", 150_000m, 0, 120m, 10, 200),
            ("real-estate", "apartment", "Apartment", 250_000m, 0, 200m, 20, 72),
            ("real-estate", "building", "Commercial building", 500_000m, 4, 400m, 30, 96),

            ("shopping-mall", "retail-unit", "Shop unit", 100_000m, 0, 80m, 10, 96),
            ("shopping-mall", "parking-deck", "Parking deck", 600_000m, 4, 480m, 20, 120),
            ("shopping-mall", "food-court", "Food court", 800_000m, 6, 650m, 30, 72),
            ("shopping-mall", "cinema", "Cinema", 1_500_000m, 10, 1_200m, 40, 96),

            ("office-tower", "floor", "Office floor", 1_000_000m, 0, 800m, 10, 120),
            ("office-tower", "smart-building", "Smart building system", 5_000_000m, 15, 4_000m, 30, 150),

            ("airport", "gate", "Gate", 10_000_000m, 0, 9_000m, 10, 96),
            ("airport", "lounge", "Lounge", 8_000_000m, 2, 7_000m, 20, 120),
            ("airport", "duty-free", "Duty-free shop", 20_000_000m, 3, 17_000m, 30, 120),
            ("airport", "terminal", "Terminal", 50_000_000m, 4, 120_000m, 40, 150),
        ];
    }
}
