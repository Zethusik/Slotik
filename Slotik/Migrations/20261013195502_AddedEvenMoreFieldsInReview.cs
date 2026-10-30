using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class AddedEvenMoreFieldsInReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMin",
                table: "Reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Reviews",
                type: "numeric",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 940001,
                columns: new[] { "DurationMin", "Price" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 940002,
                columns: new[] { "DurationMin", "Price" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMin",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Reviews");
        }
    }
}
