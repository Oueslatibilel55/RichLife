using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManagerName",
                table: "businesses",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManagerNameId",
                table: "businesses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "manager_names",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manager_names", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_businesses_ManagerNameId",
                table: "businesses",
                column: "ManagerNameId");

            migrationBuilder.CreateIndex(
                name: "IX_manager_names_Name",
                table: "manager_names",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_businesses_manager_names_ManagerNameId",
                table: "businesses",
                column: "ManagerNameId",
                principalTable: "manager_names",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            SeedManagerNames(migrationBuilder);

            // Businesses automated before names existed get one at random, so no hired
            // manager is left nameless. random() is volatile: evaluated once per row.
            migrationBuilder.Sql("""
                UPDATE businesses b
                SET "ManagerNameId" = m."Id", "ManagerName" = m."Name"
                FROM (SELECT b2."Id" AS bid,
                             (SELECT "Id" FROM manager_names ORDER BY random() + 0 * length(b2."Name") LIMIT 1) AS mid
                      FROM businesses b2 WHERE b2."IsAutomated" AND b2."ManagerNameId" IS NULL) pick
                JOIN manager_names m ON m."Id" = pick.mid
                WHERE b."Id" = pick.bid;
                """);
        }

        // Content, seeded like the business catalogue (InsertData, not HasData): from here
        // on the database owns the list. Add more with a plain INSERT — the id is identity.
        private static readonly string[] Names =
        [
            "Lucy", "Milo", "Daisy", "Oscar", "Luna", "Leo", "Bella", "Charlie", "Coco", "Teddy",
            "Rosie", "Max", "Lily", "Ollie", "Ruby", "Finn", "Mia", "Jasper", "Poppy", "Benny",
            "Hazel", "Toby", "Ivy", "Archie", "Willow", "Buddy", "Penny", "Felix", "Stella", "Louie",
            "Maisie", "Otis", "Nala", "Ziggy", "Pippa", "Rocco", "Clover", "Sunny", "Gigi", "Biscuit",
            "Honey", "Peanut", "Cookie", "Maple", "Pepper", "Mochi", "Bubbles", "Sprinkles", "Cupcake", "Muffin",
            "Noodle", "Pumpkin", "Waffles", "Truffle", "Cinnamon", "Hugo", "Jojo", "Kiki", "Lulu", "Momo",
            "Nino", "Pip", "Remy", "Tilly", "Winnie", "Zara", "Bonnie", "Freddie", "Elsie", "Alfie",
            "Lottie", "Bobby", "Millie", "Frankie", "Nellie", "Harvey", "Evie", "Arlo", "Cleo", "Dexter",
            "Fifi", "Gus", "Hattie", "Izzy", "Jack", "Kitty", "Lenny", "Mabel", "Nico", "Olive",
            "Percy", "Queenie", "Rufus", "Sadie", "Theo", "Ursula", "Vinnie", "Wren", "Yoyo", "Zuzu",
            "Amira", "Yasmine", "Nour", "Lina", "Sami", "Rayan", "Ines", "Selim", "Adam", "Malek",
            "Jade", "Chloe", "Manon", "Lou", "Noah", "Gabin", "Rose", "Juliette", "Marius", "Elio",
            "Kenzo", "Yuki", "Hana", "Sora", "Mei", "Kai", "Aria", "Nova", "Sky", "Bijou",
        ];

        private static void SeedManagerNames(MigrationBuilder migrationBuilder)
        {
            var values = new object[Names.Length, 1];
            for (var i = 0; i < Names.Length; i++) values[i, 0] = Names[i];

            migrationBuilder.InsertData(
                table: "manager_names",
                columns: ["Name"],
                values: values);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_businesses_manager_names_ManagerNameId",
                table: "businesses");

            migrationBuilder.DropTable(
                name: "manager_names");

            migrationBuilder.DropIndex(
                name: "IX_businesses_ManagerNameId",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "ManagerName",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "ManagerNameId",
                table: "businesses");
        }
    }
}
