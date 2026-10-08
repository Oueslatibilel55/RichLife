using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LuxuryCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LuxuryAssets_CompanyId",
                table: "LuxuryAssets");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "LuxuryAssets",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "IncomeMultiplierBonus",
                table: "LuxuryAssets",
                type: "numeric(20,6)",
                precision: 20,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "Cost",
                table: "LuxuryAssets",
                type: "numeric(20,4)",
                precision: 20,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<string>(
                name: "CatalogueId",
                table: "LuxuryAssets",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "LuxuryAssets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ImageCredit",
                table: "LuxuryAssets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "LuxuryAssets",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "luxury_catalogue",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                    RequiredPrestige = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ImageCredit = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImageSourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_luxury_catalogue", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LuxuryAssets_CatalogueId",
                table: "LuxuryAssets",
                column: "CatalogueId");

            migrationBuilder.CreateIndex(
                name: "IX_LuxuryAssets_CompanyId_CatalogueId",
                table: "LuxuryAssets",
                columns: new[] { "CompanyId", "CatalogueId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LuxuryAssets_luxury_catalogue_CatalogueId",
                table: "LuxuryAssets",
                column: "CatalogueId",
                principalTable: "luxury_catalogue",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            SeedLuxuryCatalogue(migrationBuilder);
        }

        // The 15 launch items. Photos are Wikimedia Commons files stored in the frontend at
        // public/luxury/; the credit (author · license) is required by their licenses and is
        // shown with every photo. InsertData, not HasData: from here the database owns it.
        private static void SeedLuxuryCatalogue(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "luxury_catalogue",
                columns: ["Id", "Name", "Category", "Description", "Price", "RequiredPrestige",
                          "ImageUrl", "ImageCredit", "ImageSourceUrl", "DisplayOrder", "IsActive"],
                values: new object[,]
            {
                { "rolex-submariner", "Rolex Submariner", 1, "The diver's watch every banker wears to the office.", 150000m, 2, "/luxury/rolex-submariner.jpg", "Eternalsleeper · Public domain", "https://commons.wikimedia.org/wiki/File:Rolex_Oyster_Perpetual_Date_Submariner_Watch.JPG", 10, true },
                { "ducati-superbike", "Ducati Panigale V2", 2, "Italian superbike. Loud, red, and completely unnecessary.", 300000m, 2, "/luxury/ducati-superbike.jpg", "Terragio67 · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Ducati_955_Panigale_V2_(IMG_9851).jpg", 20, true },
                { "porsche-911", "Porsche 911 Turbo S", 3, "The everyday supercar for the self-made entrepreneur.", 2500000m, 3, "/luxury/porsche-911.jpg", "Alexander Migl · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Porsche_992_Turbo_S_1X7A0411.jpg", 30, true },
                { "city-penthouse", "Waterfront penthouse", 4, "Glass walls, a private terrace and the whole bay at your feet.", 6000000m, 3, "/luxury/city-penthouse.jpg", "XO3D LTD · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Penthouse_Rendering.jpg", 40, true },
                { "rolls-royce-phantom", "Rolls-Royce Phantom", 3, "Arrive quietly. Everyone will still turn around.", 18000000m, 4, "/luxury/rolls-royce-phantom.jpg", "Yu Chu Chin · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Rolls-Royce_Phantom_VIII_Series_I_(Crown_Prince_of_Brunei).jpg", 50, true },
                { "lamborghini-aventador", "Lamborghini Aventador SVJ", 3, "A V12 that sounds like money leaving.", 25000000m, 4, "/luxury/lamborghini-aventador.jpg", "Calreyn88 · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Lamborghini_Aventador_SVJ_13.jpg", 60, true },
                { "beach-villa", "Modern villa with pool", 4, "Infinity pool, open-plan living, and a view nobody else gets.", 60000000m, 4, "/luxury/beach-villa.jpg", "Tuantranseo · CC BY 4.0", "https://commons.wikimedia.org/wiki/File:3D_Rendering_of_Modern_Luxury_Villa_Exterior_with_Pool.jpg", 70, true },
                { "private-helicopter", "Private helicopter", 7, "Skip the traffic. Skip the airport. Land on the roof.", 150000000m, 5, "/luxury/private-helicopter.jpg", "Matthias Zepper · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Zepper-BK_117-C2-(EC145)-SchweizerischeRettungsflugwacht.jpg", 80, true },
                { "superyacht", "Superyacht", 6, "Five decks, a helipad and a crew that knows your name.", 400000000m, 5, "/luxury/superyacht.jpg", "Getsijas · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Luxury_yacht.jpg", 90, true },
                { "private-jet", "Gulfstream G650", 7, "Nonstop to anywhere, on your schedule.", 650000000m, 5, "/luxury/private-jet.jpg", "Bene Riobó · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:EC-MLR_Gulfstream_G650_SCQ_03.jpg", 100, true },
                { "bugatti-chiron", "Bugatti Chiron", 3, "1,500 horsepower. Mostly for parking outside.", 1500000000m, 6, "/luxury/bugatti-chiron.jpg", "Matti Blume · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Bugatti_Chiron,_GIMS_2018,_Le_Grand-Saconnex_(1X7A1765).jpg", 110, true },
                { "french-chateau", "Loire Valley château", 4, "Four hundred rooms, a forest, and a staircase by Leonardo.", 3000000000m, 6, "/luxury/french-chateau.jpg", "Benh LIEU SONG · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Chambord_Castle_Northwest_facade.jpg", 120, true },
                { "private-island", "Private island in the Maldives", 5, "Your own lagoon, your own beach, your own rules.", 5000000000m, 6, "/luxury/private-island.jpg", "Dr. Ondřej Havelka (cestovatel) · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:1_Maldives,_Indian_Ocean,_Asia.jpg", 130, true },
                { "boeing-747-private", "Private Boeing 747-8", 7, "A flying palace — bedroom, office and dining hall included.", 25000000000m, 7, "/luxury/boeing-747-private.jpg", "BWard 1997 · CC BY 4.0", "https://commons.wikimedia.org/wiki/File:Royal_Flight_of_Oman_Boeing_747-8H0(BBJ)_A4O-HMS_11-8-2024.jpg", 140, true },
                { "gigayacht", "Gigayacht", 6, "Longer than a football pitch. The final word in showing off.", 40000000000m, 7, "/luxury/gigayacht.jpg", "ChrisKarsten · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Azzam_bei_L%C3%BCrssen.JPG", 150, true },
            });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LuxuryAssets_luxury_catalogue_CatalogueId",
                table: "LuxuryAssets");

            migrationBuilder.DropTable(
                name: "luxury_catalogue");

            migrationBuilder.DropIndex(
                name: "IX_LuxuryAssets_CatalogueId",
                table: "LuxuryAssets");

            migrationBuilder.DropIndex(
                name: "IX_LuxuryAssets_CompanyId_CatalogueId",
                table: "LuxuryAssets");

            migrationBuilder.DropColumn(
                name: "CatalogueId",
                table: "LuxuryAssets");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "LuxuryAssets");

            migrationBuilder.DropColumn(
                name: "ImageCredit",
                table: "LuxuryAssets");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "LuxuryAssets");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "LuxuryAssets",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<decimal>(
                name: "IncomeMultiplierBonus",
                table: "LuxuryAssets",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)",
                oldPrecision: 20,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "Cost",
                table: "LuxuryAssets",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,4)",
                oldPrecision: 20,
                oldScale: 4);

            migrationBuilder.CreateIndex(
                name: "IX_LuxuryAssets_CompanyId",
                table: "LuxuryAssets",
                column: "CompanyId");
        }
    }
}
