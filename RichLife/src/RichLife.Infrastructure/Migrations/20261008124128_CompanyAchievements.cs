using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompanyAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_achievements",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_achievements", x => new { x.CompanyId, x.Code });
                    table.ForeignKey(
                        name: "FK_company_achievements_companies_CompanyId",
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
                name: "company_achievements");
        }
    }
}
