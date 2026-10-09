using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class RemoveServiceGroupShadowCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceGroups_Categories_CategoryId1",
                table: "ServiceGroups");

            migrationBuilder.DropIndex(
                name: "IX_ServiceGroups_CategoryId1",
                table: "ServiceGroups");

            migrationBuilder.DropColumn(
                name: "CategoryId1",
                table: "ServiceGroups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryId1",
                table: "ServiceGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceGroups_CategoryId1",
                table: "ServiceGroups",
                column: "CategoryId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceGroups_Categories_CategoryId1",
                table: "ServiceGroups",
                column: "CategoryId1",
                principalTable: "Categories",
                principalColumn: "Id");
        }
    }
}
