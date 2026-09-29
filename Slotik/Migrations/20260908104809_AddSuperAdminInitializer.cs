using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminInitializer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: -1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Email", "FirstName", "LastName", "PasswordHash", "Phone", "Role", "TelegramChatId" },
                values: new object[] { -1, "superadmin@slotik.local", "Super", "Admin", "d357150517d3e65ae84985f7b705ad99fdc38372a22ecea0cecaf8aaf820a249", "+380000000000", 2, null });
        }
    }
}
