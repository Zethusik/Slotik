using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Services",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPopular",
                table: "Services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ServiceGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId1 = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceGroups_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceGroups_Categories_CategoryId1",
                        column: x => x.CategoryId1,
                        principalTable: "Categories",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "ServiceGroups",
                columns: new[] { "Id", "CategoryId", "CategoryId1", "Name" },
                values: new object[,]
                {
                    { 1, 1, null, "Манікюр" },
                    { 2, 1, null, "Педикюр" },
                    { 3, 1, null, "Додатково" }
                });

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 920001,
                columns: new[] { "GroupId", "IsPopular", "SortOrder" },
                values: new object[] { 1, true, 2 });

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 920002,
                columns: new[] { "GroupId", "IsPopular", "SortOrder" },
                values: new object[] { 1, true, 1 });

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 920003,
                columns: new[] { "GroupId", "IsPopular", "SortOrder" },
                values: new object[] { 2, false, 3 });

            migrationBuilder.CreateIndex(
                name: "IX_Services_GroupId",
                table: "Services",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceGroups_CategoryId",
                table: "ServiceGroups",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceGroups_CategoryId1",
                table: "ServiceGroups",
                column: "CategoryId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Services_ServiceGroups_GroupId",
                table: "Services",
                column: "GroupId",
                principalTable: "ServiceGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Services_ServiceGroups_GroupId",
                table: "Services");

            migrationBuilder.DropTable(
                name: "ServiceGroups");

            migrationBuilder.DropIndex(
                name: "IX_Services_GroupId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "IsPopular",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Services");
        }
    }
}
