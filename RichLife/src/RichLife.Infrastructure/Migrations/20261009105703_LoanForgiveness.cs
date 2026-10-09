using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoanForgiveness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ForgivenAmount",
                table: "loans",
                type: "numeric(24,4)",
                precision: 24,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForgivenAmount",
                table: "loans");
        }
    }
}
