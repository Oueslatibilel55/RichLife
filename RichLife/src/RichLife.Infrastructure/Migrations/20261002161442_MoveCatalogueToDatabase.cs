using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveCatalogueToDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "catalogue_businesses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Sector = table.Column<int>(type: "integer", nullable: false),
                    RequiredPrestige = table.Column<int>(type: "integer", nullable: false),
                    OpeningCost = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    BaseIncomePerSecond = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    MonthlySalaryCost = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    BaseEmployeeCount = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogue_businesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "catalogue_assets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    BusinessCatalogueId = table.Column<string>(type: "character varying(60)", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    UnlockAtAssetCount = table.Column<int>(type: "integer", nullable: false),
                    FixedIncomePerSecond = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogue_assets", x => new { x.BusinessCatalogueId, x.Id });
                    table.ForeignKey(
                        name: "FK_catalogue_assets_catalogue_businesses_BusinessCatalogueId",
                        column: x => x.BusinessCatalogueId,
                        principalTable: "catalogue_businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Rows first: the foreign key below validates every existing business against them.
            SeedCatalogue(migrationBuilder);

            migrationBuilder.CreateIndex(
                name: "IX_businesses_CatalogueId",
                table: "businesses",
                column: "CatalogueId");

            migrationBuilder.AddForeignKey(
                name: "FK_businesses_catalogue_businesses_CatalogueId",
                table: "businesses",
                column: "CatalogueId",
                principalTable: "catalogue_businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_businesses_catalogue_businesses_CatalogueId",
                table: "businesses");

            migrationBuilder.DropTable(
                name: "catalogue_assets");

            migrationBuilder.DropTable(
                name: "catalogue_businesses");

            migrationBuilder.DropIndex(
                name: "IX_businesses_CatalogueId",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "players");
        }

        // The catalogue as it stood in code (Domain/Catalogue/BusinessCatalogue.cs) when it
        // moved to the database — row for row, so players see no change. Seeded here and not
        // with HasData: HasData would keep the content in the model, and every admin edit
        // would then fight the next migration. From here on the database owns it.
        private static readonly DateTime SeededAt = new(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc);

        private static void SeedCatalogue(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "catalogue_businesses",
                columns: ["Id", "Name", "Sector", "RequiredPrestige", "OpeningCost", "BaseIncomePerSecond",
                          "MonthlySalaryCost", "BaseEmployeeCount", "Description", "DisplayOrder", "IsActive",
                          "CreatedAt", "UpdatedAt"],
                values: new object[,]
            {
                { "food-cart", "Food Cart", 5 /* Hospitality */, 1 /* TheHustle */, 500m, 2m, 0m, 0, "A simple street food cart. Low cost, instant income.", 10, true, SeededAt, SeededAt },
                { "bike-courier", "Bike Courier Service", 1 /* Transport */, 1 /* TheHustle */, 1200m, 3m, 0m, 0, "Deliver packages by bike. No employees needed.", 20, true, SeededAt, SeededAt },
                { "car-wash", "Car Wash", 7 /* Services */, 1 /* TheHustle */, 3000m, 5m, 0m, 1, "Manual car wash. Clean cars for steady income.", 30, true, SeededAt, SeededAt },
                { "parking-lot", "Parking Lot", 7 /* Services */, 1 /* TheHustle */, 5000m, 8m, 0m, 1, "Manage a parking lot in a busy area.", 40, true, SeededAt, SeededAt },
                { "vending", "Vending Machines", 7 /* Services */, 1 /* TheHustle */, 12000m, 15m, 0m, 0, "Place machines in busy areas. Fully passive.", 50, true, SeededAt, SeededAt },
                { "mini-market", "Mini-Market", 5 /* Hospitality */, 2 /* SmallBusiness */, 15000m, 20m, 1600m, 2, "A neighborhood grocery store.", 10, true, SeededAt, SeededAt },
                { "laundromat", "Laundromat", 7 /* Services */, 2 /* SmallBusiness */, 20000m, 25m, 800m, 1, "Self-service laundry. Low maintenance.", 20, true, SeededAt, SeededAt },
                { "taxi-fleet", "Taxi Fleet", 1 /* Transport */, 2 /* SmallBusiness */, 50000m, 30m, 4800m, 4, "Run a taxi and rideshare fleet.", 30, true, SeededAt, SeededAt },
                { "gym", "Gym", 7 /* Services */, 2 /* SmallBusiness */, 60000m, 40m, 2700m, 3, "A fitness gym with monthly memberships.", 40, true, SeededAt, SeededAt },
                { "transport-company", "Transport Company", 1 /* Transport */, 3 /* Entrepreneur */, 100000m, 0m, 0m, 0, "Buy trucks. Income = truck value × 0.0003.", 10, true, SeededAt, SeededAt },
                { "rent-a-car", "Rent-A-Car Agency", 1 /* Transport */, 3 /* Entrepreneur */, 80000m, 0m, 2400m, 3, "Rent cars. Income = car value × 0.0003.", 20, true, SeededAt, SeededAt },
                { "restaurant-chain", "Restaurant Chain", 5 /* Hospitality */, 3 /* Entrepreneur */, 150000m, 100m, 8000m, 8, "Open restaurant locations across the city.", 30, true, SeededAt, SeededAt },
                { "small-hotel", "Small Hotel", 2 /* RealEstate */, 3 /* Entrepreneur */, 200000m, 150m, 9000m, 10, "A 30-room hotel.", 40, true, SeededAt, SeededAt },
                { "real-estate", "Real Estate Development", 2 /* RealEstate */, 4 /* BusinessMogul */, 1000000m, 800m, 20000m, 20, "Develop and rent commercial buildings.", 10, true, SeededAt, SeededAt },
                { "shopping-mall", "Shopping Mall", 2 /* RealEstate */, 4 /* BusinessMogul */, 3000000m, 2000m, 50000m, 50, "Own and operate a shopping mall.", 20, true, SeededAt, SeededAt },
                { "office-tower", "Office Tower", 2 /* RealEstate */, 5 /* Tycoon */, 15000000m, 10000m, 200000m, 200, "A 30-floor office tower.", 10, true, SeededAt, SeededAt },
                { "airport", "International Airport", 1 /* Transport */, 5 /* Tycoon */, 200000000m, 150000m, 2000000m, 2000, "Build and operate an international airport.", 20, true, SeededAt, SeededAt },
            });

            migrationBuilder.InsertData(
                table: "catalogue_assets",
                columns: ["Id", "BusinessCatalogueId", "Name", "Price", "UnlockAtAssetCount",
                          "FixedIncomePerSecond", "DisplayOrder"],
                values: new object[,]
            {
                { "menu-item", "food-cart", "Menu item", 200m, 0, 0.5m, 10 },
                { "menu-item-2", "food-cart", "Premium dish", 500m, 5, 1.5m, 20 },
                { "bike", "bike-courier", "Delivery bike", 800m, 0, 2m, 10 },
                { "ebike", "bike-courier", "Electric bike", 2500m, 5, 6m, 20 },
                { "wash-kit", "car-wash", "Wash equipment kit", 1500m, 0, 2m, 10 },
                { "auto-wash", "car-wash", "Automatic system", 15000m, 5, 12m, 20 },
                { "signage", "parking-lot", "Signage + barrier", 2000m, 0, 3m, 10 },
                { "cameras", "parking-lot", "Security cameras", 5000m, 3, 7m, 20 },
                { "machine", "vending", "Vending machine", 2000m, 0, 8m, 10 },
                { "machine-xl", "vending", "XL machine", 6000m, 5, 20m, 20 },
                { "stock-basic", "mini-market", "Basic stock", 5000m, 0, 8m, 10 },
                { "stock-premium", "mini-market", "Premium stock", 12000m, 5, 20m, 20 },
                { "machine-std", "laundromat", "Standard washer", 3000m, 0, 7m, 10 },
                { "machine-pro", "laundromat", "Pro washer", 8000m, 4, 18m, 20 },
                { "dacia", "taxi-fleet", "Dacia Logan", 8000m, 0, null, 10 },
                { "renault", "taxi-fleet", "Renault Clio", 12000m, 3, null, 20 },
                { "toyota", "taxi-fleet", "Toyota Corolla", 20000m, 6, null, 30 },
                { "bmw3", "taxi-fleet", "BMW 3 Series", 55000m, 10, null, 40 },
                { "equipment-basic", "gym", "Basic equipment floor", 20000m, 0, 15m, 10 },
                { "equipment-pro", "gym", "Pro equipment floor", 40000m, 3, 30m, 20 },
                { "pool", "gym", "Swimming pool", 80000m, 6, 60m, 30 },
                { "fiat-fiorino", "transport-company", "Fiat Fiorino van", 25000m, 0, null, 10 },
                { "iveco-daily", "transport-company", "Iveco Daily truck", 60000m, 0, null, 20 },
                { "mercedes-actros", "transport-company", "Mercedes Actros", 100000m, 5, null, 30 },
                { "volvo-fh", "transport-company", "Volvo FH Semi", 150000m, 10, null, 40 },
                { "man-tgx", "transport-company", "MAN TGX Heavy", 200000m, 15, null, 50 },
                { "scania-r", "transport-company", "Scania R Series", 280000m, 20, null, 60 },
                { "fiat-punto", "rent-a-car", "Fiat Punto", 8000m, 0, null, 10 },
                { "renault-clio", "rent-a-car", "Renault Clio", 12000m, 0, null, 20 },
                { "vw-passat", "rent-a-car", "Volkswagen Passat", 35000m, 5, null, 30 },
                { "bmw-3", "rent-a-car", "BMW 3 Series", 55000m, 10, null, 40 },
                { "mercedes-e", "rent-a-car", "Mercedes E-Class", 80000m, 15, null, 50 },
                { "porsche", "rent-a-car", "Porsche Cayenne", 120000m, 20, null, 60 },
                { "location", "restaurant-chain", "New restaurant location", 60000m, 0, 80m, 10 },
                { "room-std", "small-hotel", "Standard room", 5000m, 0, 4m, 10 },
                { "room-suite", "small-hotel", "Suite", 15000m, 10, 12m, 20 },
                { "building", "real-estate", "Commercial building", 500000m, 0, 400m, 10 },
                { "retail-unit", "shopping-mall", "Retail unit", 100000m, 0, 80m, 10 },
                { "floor", "office-tower", "Office floor", 1000000m, 0, 800m, 10 },
                { "terminal", "airport", "Terminal", 50000000m, 0, 120000m, 10 },
            });
        }
    }
}
