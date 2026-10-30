using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionTrialFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTrial",
                table: "Subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 900001,
                column: "IsTrial",
                value: false);

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 900002,
                column: "IsTrial",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTrial",
                table: "Subscriptions");
        }
    }
}
