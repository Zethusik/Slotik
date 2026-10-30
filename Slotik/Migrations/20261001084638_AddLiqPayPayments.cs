using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class AddLiqPayPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PaidAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Payments",
                type: "text",
                nullable: false,
                defaultValue: "UAH");

            migrationBuilder.AddColumn<string>(
                name: "OrderId",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPaymentId",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStatus",
                table: "Payments",
                type: "text",
                nullable: true);

            // LiqPay:
            // Для старых платежей создаём уникальный OrderId,
            // чтобы UNIQUE INDEX не упал.
            migrationBuilder.Sql("""
                UPDATE "Payments"
                SET "OrderId" = 'legacy-payment-' || "Id"
                WHERE "OrderId" IS NULL;
            """);

            migrationBuilder.AlterColumn<string>(
                name: "OrderId",
                table: "Payments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderPaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderStatus",
                table: "Payments");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PaidAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
