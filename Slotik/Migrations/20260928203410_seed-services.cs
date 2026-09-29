using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class seedservices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 920001,
                columns: new[] { "Description", "Name", "Price" },
                values: new object[] { "Манікюр + гель-лак", "Манікюр + гель-лак", 300m });

            migrationBuilder.InsertData(
                table: "Services",
                columns: new[] { "Id", "Description", "DurationMin", "Included", "MasterId", "Name", "Price" },
                values: new object[,]
                {
                    { 920002, "Манікюр + гель", 60, "Консультація та виконання послуги", 900001, "Манікюр + гель", 250m },
                    { 920003, "Педикюр", 30, "Консультація та виконання послуги", 900001, "Педикюр", 801m }
                });

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 900002,
                columns: new[] { "ExpiresAt", "Plan" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 9, 24, 23, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 920002);

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 920003);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 920001,
                columns: new[] { "Description", "Name", "Price" },
                values: new object[] { "Тестова послуга для перевірки записів майстра.", "Тестова послуга", 800m });

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 900002,
                columns: new[] { "ExpiresAt", "Plan" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 9, 20, 23, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0 });
        }
    }
}
