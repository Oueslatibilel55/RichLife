using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Bourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_stocks",
                columns: table => new
                {
                    Ticker = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Shares = table.Column<long>(type: "bigint", nullable: false),
                    CostBasis = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_stocks", x => new { x.CompanyId, x.Ticker });
                    table.ForeignKey(
                        name: "FK_company_stocks_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_stocks");
        }
    }
}
