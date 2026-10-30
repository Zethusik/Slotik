using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class AddProTrial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProTrialUsedAt",
                table: "Masters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Masters",
                keyColumn: "Id",
                keyValue: 900001,
                column: "ProTrialUsedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "Masters",
                keyColumn: "Id",
                keyValue: 900002,
                column: "ProTrialUsedAt",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProTrialUsedAt",
                table: "Masters");
        }
    }
}
