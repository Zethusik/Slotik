using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class BoundPendingBookingLifetime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PendingExpiresAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            // Existing Pending holds receive at most 24 more hours, never beyond the visit.
            migrationBuilder.Sql("""
                UPDATE "Bookings"
                SET "PendingExpiresAt" = LEAST("StartsAt", CURRENT_TIMESTAMP + INTERVAL '24 hours')
                WHERE "Status" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingExpiresAt",
                table: "Bookings");
        }
    }
}
