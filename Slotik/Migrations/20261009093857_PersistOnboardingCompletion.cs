using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class PersistOnboardingCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnboardingCompleted",
                table: "Masters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Conservative backfill: all current profile steps and a provably successful,
            // currently active activation. Pending/failed/history-only rows stay false.
            migrationBuilder.Sql("""
                UPDATE "Masters" AS m SET "IsOnboardingCompleted" = TRUE
                WHERE m."About" IS NOT NULL AND btrim(m."About") <> ''
                  AND EXISTS (SELECT 1 FROM "Categories" c WHERE c."Id" = m."CategoryId")
                  AND EXISTS (SELECT 1 FROM "Services" s WHERE s."MasterId" = m."Id")
                  AND EXISTS (
                    SELECT 1 FROM "Schedules" s JOIN "ScheduleIntervals" i ON i."ScheduleId" = s."Id"
                    WHERE s."MasterId" = m."Id" AND s."IsWorking")
                  AND EXISTS (
                    SELECT 1 FROM "Subscriptions" s
                    WHERE s."MasterId" = m."Id" AND s."Status" = 0 AND s."ExpiresAt" > CURRENT_TIMESTAMP
                      AND (s."Plan" = 0
                        OR (s."IsTrial" AND s."Plan" = 2 AND m."ProTrialUsedAt" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "Payments" p WHERE p."SubscriptionId" = s."Id"
                            AND p."Status" = 1 AND p."PaidAt" IS NOT NULL)));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOnboardingCompleted",
                table: "Masters");
        }
    }
}
