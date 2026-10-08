using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixEconomyRulesAndAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -- Audit columns ------------------------------------------------------
            // Existing rows get the migration timestamp rather than 0001-01-01.
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "players",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now() at time zone 'utc'");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "companies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now() at time zone 'utc'");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "companies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now() at time zone 'utc'");

            // -- Businesses now carry their catalogue key ---------------------------
            migrationBuilder.AddColumn<string>(
                name: "CatalogueId",
                table: "businesses",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            // Backfill from the catalogue. Names are unique across BusinessCatalogue,
            // so this is an exact mapping for every row opened by the old code.
            migrationBuilder.Sql("""
                UPDATE businesses SET "CatalogueId" = 'food-cart' WHERE "CatalogueId" = '' AND "Name" = 'Food Cart';
                UPDATE businesses SET "CatalogueId" = 'bike-courier' WHERE "CatalogueId" = '' AND "Name" = 'Bike Courier Service';
                UPDATE businesses SET "CatalogueId" = 'car-wash' WHERE "CatalogueId" = '' AND "Name" = 'Car Wash';
                UPDATE businesses SET "CatalogueId" = 'parking-lot' WHERE "CatalogueId" = '' AND "Name" = 'Parking Lot';
                UPDATE businesses SET "CatalogueId" = 'vending' WHERE "CatalogueId" = '' AND "Name" = 'Vending Machines';
                UPDATE businesses SET "CatalogueId" = 'mini-market' WHERE "CatalogueId" = '' AND "Name" = 'Mini-Market';
                UPDATE businesses SET "CatalogueId" = 'laundromat' WHERE "CatalogueId" = '' AND "Name" = 'Laundromat';
                UPDATE businesses SET "CatalogueId" = 'taxi-fleet' WHERE "CatalogueId" = '' AND "Name" = 'Taxi Fleet';
                UPDATE businesses SET "CatalogueId" = 'gym' WHERE "CatalogueId" = '' AND "Name" = 'Gym';
                UPDATE businesses SET "CatalogueId" = 'transport-company' WHERE "CatalogueId" = '' AND "Name" = 'Transport Company';
                UPDATE businesses SET "CatalogueId" = 'rent-a-car' WHERE "CatalogueId" = '' AND "Name" = 'Rent-A-Car Agency';
                UPDATE businesses SET "CatalogueId" = 'restaurant-chain' WHERE "CatalogueId" = '' AND "Name" = 'Restaurant Chain';
                UPDATE businesses SET "CatalogueId" = 'small-hotel' WHERE "CatalogueId" = '' AND "Name" = 'Small Hotel';
                UPDATE businesses SET "CatalogueId" = 'real-estate' WHERE "CatalogueId" = '' AND "Name" = 'Real Estate Development';
                UPDATE businesses SET "CatalogueId" = 'shopping-mall' WHERE "CatalogueId" = '' AND "Name" = 'Shopping Mall';
                UPDATE businesses SET "CatalogueId" = 'office-tower' WHERE "CatalogueId" = '' AND "Name" = 'Office Tower';
                UPDATE businesses SET "CatalogueId" = 'airport' WHERE "CatalogueId" = '' AND "Name" = 'International Airport';

                -- Anything the catalogue no longer knows about keeps a unique, inert key
                -- so the row survives the unique index below.
                UPDATE businesses SET "CatalogueId" = 'legacy-' || "Id" WHERE "CatalogueId" = '';

                -- The old code allowed opening the same business twice. Keep the oldest
                -- copy on the real catalogue key and park the rest under a unique one.
                UPDATE businesses SET "CatalogueId" = 'legacy-' || "Id"
                WHERE "Id" IN (
                    SELECT "Id" FROM (
                        SELECT "Id", row_number() OVER (
                            PARTITION BY "CompanyId", "CatalogueId" ORDER BY "CreatedAt", "Id"
                        ) AS rn
                        FROM businesses
                    ) ranked
                    WHERE ranked.rn > 1
                );
                """);

            // -- Indexes ------------------------------------------------------------
            migrationBuilder.DropIndex(
                name: "IX_businesses_CompanyId",
                table: "businesses");

            migrationBuilder.CreateIndex(
                name: "IX_businesses_CompanyId_CatalogueId",
                table: "businesses",
                columns: ["CompanyId", "CatalogueId"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_players_RefreshToken",
                table: "players",
                column: "RefreshToken");

            migrationBuilder.CreateIndex(
                name: "IX_companies_AllTimeEarnings",
                table: "companies",
                column: "AllTimeEarnings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_players_RefreshToken",
                table: "players");

            migrationBuilder.DropIndex(
                name: "IX_companies_AllTimeEarnings",
                table: "companies");

            migrationBuilder.DropIndex(
                name: "IX_businesses_CompanyId_CatalogueId",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "players");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "CatalogueId",
                table: "businesses");

            migrationBuilder.CreateIndex(
                name: "IX_businesses_CompanyId",
                table: "businesses",
                column: "CompanyId");
        }
    }
}
