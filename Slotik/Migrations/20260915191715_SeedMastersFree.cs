using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class SeedMastersFree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Icon", "IsHiddenFromCatalog", "Name" },
                values: new object[,]
                {
                    { 1, "hand-finger", false, "Манікюр" },
                    { 2, "scissors", false, "Перукар" },
                    { 3, "sparkles", false, "Візаж" },
                    { 4, "eye", false, "Брови та вії" },
                    { 5, "needle", false, "Тату" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "FirstName", "LastName", "PasswordHash", "Phone", "Role", "TelegramChatId" },
                values: new object[,]
                {
                    { 900001, new DateTimeOffset(new DateTime(2026, 9, 15, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "masterfree1@slotik.com", "Майстер", "Free1", "2b5efdd05f1be04909fd37f788acae6a1b039f3be38dc39ea84c1f569be1fded", "+380000000001", 1, null },
                    { 900002, new DateTimeOffset(new DateTime(2026, 9, 15, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "masterfree2@slotik.com", "Майстер", "Free2", "2b5efdd05f1be04909fd37f788acae6a1b039f3be38dc39ea84c1f569be1fded", "+380000000002", 1, null }
                });

            migrationBuilder.InsertData(
                table: "Masters",
                columns: new[] { "Id", "About", "CategoryId", "DistrictId", "ExperienceYears", "IsBlocked", "SlotStepMin", "Slug", "UserId" },
                values: new object[,]
                {
                    { 900001, "Тестовий майстер з Free тарифом", 1, 1, 3, false, 30, "master-free-1", 900001 },
                    { 900002, "Тестовий майстер з Free тарифом", 1, 1, 5, false, 30, "master-free-2", 900002 }
                });

            migrationBuilder.InsertData(
                table: "Subscriptions",
                columns: new[] { "Id", "ExpiresAt", "MasterId", "Plan", "Status" },
                values: new object[,]
                {
                    { 900001, new DateTimeOffset(new DateTime(2099, 12, 31, 23, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 900001, 0, 0 },
                    { 900002, new DateTimeOffset(new DateTime(2099, 12, 31, 23, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 900002, 0, 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 900001);

            migrationBuilder.DeleteData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 900002);

            migrationBuilder.DeleteData(
                table: "Masters",
                keyColumn: "Id",
                keyValue: 900001);

            migrationBuilder.DeleteData(
                table: "Masters",
                keyColumn: "Id",
                keyValue: 900002);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900001);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900002);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 5);
        }
    }
}