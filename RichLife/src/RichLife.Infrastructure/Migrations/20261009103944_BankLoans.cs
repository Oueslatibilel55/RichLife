using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BankLoans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    BankName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BankIcon = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Principal = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    InterestRate = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    TotalRepay = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Installments = table.Column<int>(type: "integer", nullable: false),
                    InstallmentAmount = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Paid = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    Penalties = table.Column<decimal>(type: "numeric(24,4)", precision: 24, scale: 4, nullable: false),
                    MissedPayments = table.Column<int>(type: "integer", nullable: false),
                    NextPaymentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RepaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loans_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_loans_CompanyId_active",
                table: "loans",
                column: "CompanyId",
                unique: true,
                filter: "\"RepaidAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loans");
        }
    }
}
