using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <summary>
    /// Managers become 4-hour shifts: the IsAutomated flag is replaced by ManagerUntil.
    /// Reordered by hand — the generated version dropped IsAutomated first, which would
    /// have lost every hired manager. Businesses that had one get a fresh shift.
    /// </summary>
    public partial class ManagerShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ManagerUntil",
                table: "businesses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE businesses
                SET "ManagerUntil" = now() + interval '4 hours'
                WHERE "IsAutomated";
                """);

            migrationBuilder.DropColumn(
                name: "IsAutomated",
                table: "businesses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAutomated",
                table: "businesses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE businesses SET "IsAutomated" = true WHERE "ManagerUntil" > now();
                """);

            migrationBuilder.DropColumn(
                name: "ManagerUntil",
                table: "businesses");
        }
    }
}
