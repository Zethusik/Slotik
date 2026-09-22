using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class SeedReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Services",
                columns: new[] { "Id", "Description", "DurationMin", "Included", "MasterId", "Name", "Price" },
                values: new object[] { 920001, "Тестова послуга для перевірки записів майстра.", 60, "Консультація та виконання послуги", 900001, "Тестова послуга", 800m });

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 900002,
                column: "ExpiresAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 23, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "FirstName", "LastName", "PasswordHash", "Phone", "Role", "TelegramChatId" },
                values: new object[,]
                {
                    { 910001, new DateTimeOffset(new DateTime(2026, 7, 1, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "client1@slotik.test", "Олександр", "Клієнт", "598e94d875ce2d6f38c297129b5c059afe1b4f6590682b19e27c3deecf6c4140", "+380501110001", 0, null },
                    { 910002, new DateTimeOffset(new DateTime(2026, 7, 5, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "client2@slotik.test", "Марія", "Клієнт", "598e94d875ce2d6f38c297129b5c059afe1b4f6590682b19e27c3deecf6c4140", "+380501110002", 0, null },
                    { 910003, new DateTimeOffset(new DateTime(2026, 8, 1, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "client3@slotik.test", "Іван", "Клієнт", "598e94d875ce2d6f38c297129b5c059afe1b4f6590682b19e27c3deecf6c4140", "+380501110003", 0, null }
                });

            migrationBuilder.InsertData(
                table: "Bookings",
                columns: new[] { "Id", "Comment", "EndsAt", "MasterId", "ReminderSent", "ServiceId", "StartsAt", "Status", "UserId" },
                values: new object[,]
                {
                    { 930001, "Перший завершений запис клієнта.", new DateTimeOffset(new DateTime(2026, 8, 10, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 900001, true, 920001, new DateTimeOffset(new DateTime(2026, 8, 10, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 910001 },
                    { 930002, "Другий завершений запис того самого клієнта.", new DateTimeOffset(new DateTime(2026, 9, 5, 15, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 900001, true, 920001, new DateTimeOffset(new DateTime(2026, 9, 5, 14, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 910001 },
                    { 930003, "Клієнт скасував запис.", new DateTimeOffset(new DateTime(2026, 9, 12, 13, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 900001, false, 920001, new DateTimeOffset(new DateTime(2026, 9, 12, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, 910002 },
                    { 930004, "Майбутній підтверджений запис.", new DateTimeOffset(new DateTime(2027, 1, 15, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 900001, false, 920001, new DateTimeOffset(new DateTime(2027, 1, 15, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 910003 }
                });

            migrationBuilder.InsertData(
                table: "Reviews",
                columns: new[] { "Id", "BookingId", "Rating", "Text" },
                values: new object[,]
                {
                    { 940001, 930001, 5, "Все дуже сподобалось." },
                    { 940002, 930002, 4, "Хороший майстер, прийду ще." }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Bookings",
                keyColumn: "Id",
                keyValue: 930003);

            migrationBuilder.DeleteData(
                table: "Bookings",
                keyColumn: "Id",
                keyValue: 930004);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 940001);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 940002);

            migrationBuilder.DeleteData(
                table: "Bookings",
                keyColumn: "Id",
                keyValue: 930001);

            migrationBuilder.DeleteData(
                table: "Bookings",
                keyColumn: "Id",
                keyValue: 930002);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 910002);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 910003);

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 920001);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 910001);

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 900002,
                column: "ExpiresAt",
                value: new DateTimeOffset(new DateTime(2099, 12, 31, 23, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
