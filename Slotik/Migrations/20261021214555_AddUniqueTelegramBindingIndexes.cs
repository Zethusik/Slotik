using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueTelegramBindingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TelegramBindings_UserId",
                table: "TelegramBindings");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramNotification_UserId",
                table: "TelegramNotification",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramBindings_TelegramUserId",
                table: "TelegramBindings",
                column: "TelegramUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelegramBindings_UserId",
                table: "TelegramBindings",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TelegramNotification_Users_UserId",
                table: "TelegramNotification",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TelegramNotification_Users_UserId",
                table: "TelegramNotification");

            migrationBuilder.DropIndex(
                name: "IX_TelegramNotification_UserId",
                table: "TelegramNotification");

            migrationBuilder.DropIndex(
                name: "IX_TelegramBindings_TelegramUserId",
                table: "TelegramBindings");

            migrationBuilder.DropIndex(
                name: "IX_TelegramBindings_UserId",
                table: "TelegramBindings");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramBindings_UserId",
                table: "TelegramBindings",
                column: "UserId");
        }
    }
}
