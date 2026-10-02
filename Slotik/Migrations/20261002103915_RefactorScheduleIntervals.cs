using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class RefactorScheduleIntervals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ========================================
            // CREATE NEW INTERVAL TABLE
            // ========================================

            migrationBuilder.CreateTable(
                name: "ScheduleIntervals",
                columns: table => new
                {
                    Id = table.Column<int>(
                            type: "integer",
                            nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                    StartTime = table.Column<TimeOnly>(
                        type: "time without time zone",
                        nullable: false),

                    EndTime = table.Column<TimeOnly>(
                        type: "time without time zone",
                        nullable: false),

                    ScheduleId = table.Column<int>(
                        type: "integer",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_ScheduleIntervals",
                        x => x.Id);

                    table.ForeignKey(
                        name: "FK_ScheduleIntervals_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleIntervals_ScheduleId",
                table: "ScheduleIntervals",
                column: "ScheduleId");

            // ========================================
            // MIGRATE OLD SCHEDULE DATA
            // ========================================

            // Schedule without a valid break:
            // StartTime -> EndTime
            migrationBuilder.Sql(
                """
                INSERT INTO "ScheduleIntervals"
                    ("StartTime", "EndTime", "ScheduleId")
                SELECT
                    "StartTime",
                    "EndTime",
                    "Id"
                FROM "Schedules"
                WHERE
                    "IsWorking" = TRUE
                    AND "StartTime" < "EndTime"
                    AND (
                        "BreakStart" IS NULL
                        OR "BreakEnd" IS NULL
                        OR "BreakStart" <= "StartTime"
                        OR "BreakEnd" >= "EndTime"
                        OR "BreakStart" >= "BreakEnd"
                    );
                """);

            // Schedule with a valid break:
            // StartTime -> BreakStart
            migrationBuilder.Sql(
                """
                INSERT INTO "ScheduleIntervals"
                    ("StartTime", "EndTime", "ScheduleId")
                SELECT
                    "StartTime",
                    "BreakStart",
                    "Id"
                FROM "Schedules"
                WHERE
                    "IsWorking" = TRUE
                    AND "StartTime" < "EndTime"
                    AND "BreakStart" IS NOT NULL
                    AND "BreakEnd" IS NOT NULL
                    AND "StartTime" < "BreakStart"
                    AND "BreakStart" < "BreakEnd"
                    AND "BreakEnd" < "EndTime";
                """);

            // Schedule with a valid break:
            // BreakEnd -> EndTime
            migrationBuilder.Sql(
                """
                INSERT INTO "ScheduleIntervals"
                    ("StartTime", "EndTime", "ScheduleId")
                SELECT
                    "BreakEnd",
                    "EndTime",
                    "Id"
                FROM "Schedules"
                WHERE
                    "IsWorking" = TRUE
                    AND "StartTime" < "EndTime"
                    AND "BreakStart" IS NOT NULL
                    AND "BreakEnd" IS NOT NULL
                    AND "StartTime" < "BreakStart"
                    AND "BreakStart" < "BreakEnd"
                    AND "BreakEnd" < "EndTime";
                """);

            // ========================================
            // REMOVE OLD SCHEDULE STRUCTURE
            // ========================================

            migrationBuilder.DropIndex(
                name: "IX_Schedules_MasterId",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "BreakEnd",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "BreakStart",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "Schedules");

            // ========================================
            // ONE SCHEDULE PER MASTER PER WEEKDAY
            // ========================================

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_MasterId_Weekday",
                table: "Schedules",
                columns: new[]
                {
                    "MasterId",
                    "Weekday"
                },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleIntervals");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_MasterId_Weekday",
                table: "Schedules");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "BreakEnd",
                table: "Schedules",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "BreakStart",
                table: "Schedules",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "EndTime",
                table: "Schedules",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "Schedules",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_MasterId",
                table: "Schedules",
                column: "MasterId");
        }
    }
}