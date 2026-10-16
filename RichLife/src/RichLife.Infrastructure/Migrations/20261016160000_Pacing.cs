using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RichLife.Infrastructure.Persistence;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <summary>
    /// Slower game (features/016-pacing.md): incomes scaled down per prestige level, then every
    /// player reset once more — logins and company names kept, $1,000 starting cash, 0.1/s passive.
    /// No model change (prices, passive income and the new rules live in code), so no designer
    /// file: the model snapshot is unchanged.
    /// </summary>
    [DbContext(typeof(GameDbContext))]
    [Migration("20261016160000_Pacing")]
    public partial class Pacing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Income × this factor by the business's prestige level — tuned with the pacing
            // simulator. Applies to units, place kinds and the entries' base income.
            const string Factor = """
                CASE c."RequiredPrestige" WHEN 1 THEN 0.15 WHEN 2 THEN 0.07 WHEN 3 THEN 0.12 ELSE 0.15 END
                """;

            migrationBuilder.Sql($"""
                UPDATE catalogue_assets a
                SET "FixedIncomePerSecond" = round(a."FixedIncomePerSecond" * {Factor}, 6)
                FROM catalogue_businesses c
                WHERE c."Id" = a."BusinessCatalogueId" AND a."FixedIncomePerSecond" IS NOT NULL;

                UPDATE catalogue_place_kinds k
                SET "BaseIncomePerSecond" = round(k."BaseIncomePerSecond" * {Factor}, 6)
                FROM catalogue_businesses c
                WHERE c."Id" = k."BusinessCatalogueId" AND k."BaseIncomePerSecond" IS NOT NULL;

                UPDATE catalogue_businesses c
                SET "BaseIncomePerSecond" = round(c."BaseIncomePerSecond" * {Factor}, 6), "UpdatedAt" = now();
                """);

            // The reset (owner's call, 2026-10-16): as in CatalogueLikeRoadToRich, with the new
            // starting cash and passive income.
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
                    "Cash" = 1000, "AllTimeEarnings" = 0, "TaxesPaid" = 0,
                    "PrestigeLevel" = 1, "PrestigeCount" = 0, "PassiveIncomePerSecond" = 0.1,
                    "Diamonds" = 25, "BoostUntil" = NULL, "OfflineBonusAmount" = 0, "OfflineBonusUntil" = NULL,
                    "FeaturedBadgeId" = NULL, "AvatarId" = NULL,
                    "LastSyncAt" = now(), "CashOverridePending" = true, "UpdatedAt" = now();

                INSERT INTO diamond_transactions ("Id", "CompanyId", "Amount", "Balance", "Reason", "Detail", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), c."Id", 25, 25, 'welcome', NULL, now(), now() FROM companies c;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The reset cannot be undone; the income scaling is content (edit it in the admin panel).
        }
    }
}
