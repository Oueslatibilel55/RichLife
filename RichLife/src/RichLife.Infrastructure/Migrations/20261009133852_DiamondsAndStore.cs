using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DiamondsAndStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BoostUntil",
                table: "companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Diamonds",
                table: "companies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FeaturedBadgeId",
                table: "companies",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OfflineBonusAmount",
                table: "companies",
                type: "numeric(20,4)",
                precision: 20,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfflineBonusUntil",
                table: "companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "company_badges",
                columns: table => new
                {
                    BadgeId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_badges", x => new { x.CompanyId, x.BadgeId });
                    table.ForeignKey(
                        name: "FK_company_badges_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "diamond_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Balance = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Detail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diamond_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_diamond_transactions_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_diamond_transactions_CompanyId_CreatedAt",
                table: "diamond_transactions",
                columns: new[] { "CompanyId", "CreatedAt" });

            // Existing companies get what a new one would have had: the welcome gift, plus
            // the diamonds of the achievements they already unlocked — each with its ledger line.
            migrationBuilder.Sql("""
                UPDATE companies c
                SET "Diamonds" = 25 + 10 * (SELECT count(*) FROM company_achievements a WHERE a."CompanyId" = c."Id");

                INSERT INTO diamond_transactions ("Id", "CompanyId", "Amount", "Balance", "Reason", "Detail", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), c."Id", 25, 25, 'welcome', NULL, now() AT TIME ZONE 'utc', now() AT TIME ZONE 'utc'
                FROM companies c;

                INSERT INTO diamond_transactions ("Id", "CompanyId", "Amount", "Balance", "Reason", "Detail", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), c."Id", c."Diamonds" - 25, c."Diamonds", 'backfill', 'achievements',
                       now() AT TIME ZONE 'utc' + interval '1 millisecond', now() AT TIME ZONE 'utc'
                FROM companies c
                WHERE c."Diamonds" > 25;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_badges");

            migrationBuilder.DropTable(
                name: "diamond_transactions");

            migrationBuilder.DropColumn(
                name: "BoostUntil",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "Diamonds",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "FeaturedBadgeId",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "OfflineBonusAmount",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "OfflineBonusUntil",
                table: "companies");
        }
    }
}
