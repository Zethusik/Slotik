using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class TrackPaymentEntitlementsAndReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EntitlementReviewRequired",
                table: "Payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReconciledAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReconciliationAttempts",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EntitlementGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<Guid>(type: "uuid", nullable: false),
                    MasterId = table.Column<int>(type: "integer", nullable: false),
                    SourceSubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    PaymentId = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OriginalStartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OriginalEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntitlementGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntitlementGrants_Masters_MasterId",
                        column: x => x.MasterId,
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EntitlementGrants_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EntitlementGrants_Subscriptions_SourceSubscriptionId",
                        column: x => x.SourceSubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntitlementGrants_MasterId_ChainId",
                table: "EntitlementGrants",
                columns: new[] { "MasterId", "ChainId" });

            migrationBuilder.CreateIndex(
                name: "IX_EntitlementGrants_PaymentId",
                table: "EntitlementGrants",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntitlementGrants_SourceSubscriptionId",
                table: "EntitlementGrants",
                column: "SourceSubscriptionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntitlementGrants");

            migrationBuilder.DropColumn(
                name: "EntitlementReviewRequired",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "LastReconciledAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReconciliationAttempts",
                table: "Payments");
        }
    }
}
