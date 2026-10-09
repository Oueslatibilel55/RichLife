using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BusinessTaxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxesPaid",
                table: "companies",
                type: "numeric(20,4)",
                precision: 20,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxDue",
                table: "businesses",
                type: "numeric(20,4)",
                precision: 20,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "TaxPeriodStart",
                table: "businesses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "TaxableEarnings",
                table: "businesses",
                type: "numeric(24,6)",
                precision: 24,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            // Existing businesses start their first tax period now: nothing earned before
            // taxes existed is taxed.
            migrationBuilder.Sql("UPDATE businesses SET \"TaxPeriodStart\" = now();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxesPaid",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "TaxDue",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "TaxPeriodStart",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "TaxableEarnings",
                table: "businesses");
        }
    }
}
