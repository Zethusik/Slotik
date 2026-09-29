using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Slotik.Migrations
{
    /// <inheritdoc />
    public partial class InitialClean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Icon = table.Column<string>(type: "text", nullable: false),
                    IsHiddenFromCatalog = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingResets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "text", nullable: false),
                    codeHash = table.Column<string>(type: "text", nullable: false),
                    codeExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finalTokenHash = table.Column<string>(type: "text", nullable: true),
                    finalExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingResets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    TelegramChatId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Districts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CityId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Districts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Districts_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Masters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    DistrictId = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    About = table.Column<string>(type: "text", nullable: true),
                    ExperienceYears = table.Column<int>(type: "integer", nullable: false),
                    SlotStepMin = table.Column<int>(type: "integer", nullable: false),
                    IsBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Masters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Masters_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Masters_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Masters_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DaysOff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DateFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    DateTo = table.Column<DateOnly>(type: "date", nullable: false),
                    MasterId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DaysOff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DaysOff_Masters_MasterId",
                        column: x => x.MasterId,
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Favorites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    MasterId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Favorites_Masters_MasterId",
                        column: x => x.MasterId,
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Favorites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Weekday = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    BreakStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    BreakEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    IsWorking = table.Column<bool>(type: "boolean", nullable: false),
                    MasterId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schedules_Masters_MasterId",
                        column: x => x.MasterId,
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MasterId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DurationMin = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Included = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Services_Masters_MasterId",
                        column: x => x.MasterId,
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MasterId = table.Column<int>(type: "integer", nullable: false),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Masters_MasterId",
                        column: x => x.MasterId,
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    ReminderSent = table.Column<bool>(type: "boolean", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    MasterId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_Masters_MasterId",
                        column: x => x.MasterId,
                        principalTable: "Masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bookings_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bookings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServicePhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    PhotoUrl = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServicePhotos_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                table: "Cities",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Київ" },
                    { 2, "Харків" },
                    { 3, "Одеса" },
                    { 4, "Дніпро" },
                    { 5, "Львів" },
                    { 6, "Запоріжжя" },
                    { 7, "Кривий Ріг" },
                    { 8, "Миколаїв" },
                    { 9, "Маріуполь" },
                    { 10, "Вінниця" },
                    { 11, "Херсон" },
                    { 12, "Полтава" },
                    { 13, "Чернігів" },
                    { 14, "Черкаси" },
                    { 15, "Житомир" },
                    { 16, "Суми" },
                    { 17, "Кропивницький" },
                    { 18, "Кременчук" },
                    { 19, "Кам'янське" },
                    { 20, "Чернівці" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "FirstName", "LastName", "PasswordHash", "Phone", "Role", "TelegramChatId" },
                values: new object[,]
                {
                    { 900001, new DateTimeOffset(new DateTime(2026, 9, 15, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "masterfree1@slotik.com", "Майстер", "Free1", "2b5efdd05f1be04909fd37f788acae6a1b039f3be38dc39ea84c1f569be1fded", "+380000000001", 1, null },
                    { 900002, new DateTimeOffset(new DateTime(2026, 9, 15, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "masterfree2@slotik.com", "Майстер", "Free2", "2b5efdd05f1be04909fd37f788acae6a1b039f3be38dc39ea84c1f569be1fded", "+380000000002", 1, null },
                    { 910001, new DateTimeOffset(new DateTime(2026, 7, 1, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "client1@slotik.test", "Олександр", "Клієнт", "598e94d875ce2d6f38c297129b5c059afe1b4f6590682b19e27c3deecf6c4140", "+380501110001", 0, null },
                    { 910002, new DateTimeOffset(new DateTime(2026, 7, 5, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "client2@slotik.test", "Марія", "Клієнт", "598e94d875ce2d6f38c297129b5c059afe1b4f6590682b19e27c3deecf6c4140", "+380501110002", 0, null },
                    { 910003, new DateTimeOffset(new DateTime(2026, 8, 1, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "client3@slotik.test", "Іван", "Клієнт", "598e94d875ce2d6f38c297129b5c059afe1b4f6590682b19e27c3deecf6c4140", "+380501110003", 0, null }
                });

            migrationBuilder.InsertData(
                table: "Districts",
                columns: new[] { "Id", "CityId", "Name" },
                values: new object[,]
                {
                    { 1, 1, "Голосіївський" },
                    { 2, 1, "Дарницький" },
                    { 3, 1, "Деснянський" },
                    { 4, 1, "Дніпровський" },
                    { 5, 1, "Оболонський" },
                    { 6, 1, "Печерський" },
                    { 7, 1, "Подільський" },
                    { 8, 1, "Святошинський" },
                    { 9, 1, "Солом'янський" },
                    { 10, 1, "Шевченківський" },
                    { 11, 2, "Шевченківський" },
                    { 12, 2, "Київський" },
                    { 13, 2, "Салтівський" },
                    { 14, 2, "Немішлянський" },
                    { 15, 2, "Індустріальний" },
                    { 16, 2, "Слобідський" },
                    { 17, 2, "Основ'янський" },
                    { 18, 2, "Новобаварський" },
                    { 19, 2, "Холодногірський" },
                    { 20, 3, "Київський" },
                    { 21, 3, "Пересипський" },
                    { 22, 3, "Приморський" },
                    { 23, 3, "Хаджибейський" },
                    { 24, 4, "Амур-Нижньодніпровський" },
                    { 25, 4, "Індустріальний" },
                    { 26, 4, "Новокодацький" },
                    { 27, 4, "Самарський" },
                    { 28, 4, "Соборний" },
                    { 29, 4, "Центральний" },
                    { 30, 4, "Чечелівський" },
                    { 31, 4, "Шевченківський" },
                    { 32, 5, "Галицький" },
                    { 33, 5, "Залізничний" },
                    { 34, 5, "Личаківський" },
                    { 35, 5, "Сихівський" },
                    { 36, 5, "Франківський" },
                    { 37, 5, "Шевченківський" },
                    { 38, 6, "Олександрівський" },
                    { 39, 6, "Заводський" },
                    { 40, 6, "Комунарський" },
                    { 41, 6, "Дніпровський" },
                    { 42, 6, "Вознесенівський" },
                    { 43, 6, "Хортицький" },
                    { 44, 6, "Шевченківський" },
                    { 45, 7, "Довгинцівський" },
                    { 46, 7, "Покровський" },
                    { 47, 7, "Інгулецький" },
                    { 48, 7, "Металургійний" },
                    { 49, 7, "Нікопольський" },
                    { 50, 7, "Саксаганський" },
                    { 51, 7, "Тернівський" },
                    { 52, 7, "Центрально-Міський" },
                    { 53, 8, "Заводський" },
                    { 54, 8, "Корабельний" },
                    { 55, 8, "Інгульський" },
                    { 56, 8, "Центральний" },
                    { 57, 9, "Кальміуський" },
                    { 58, 9, "Лівобережний" },
                    { 59, 9, "Приморський" },
                    { 60, 9, "Центральний" },
                    { 61, 10, "Замостянський" },
                    { 62, 10, "Вишенька" },
                    { 63, 10, "Центр" },
                    { 64, 10, "П'ятничани" },
                    { 65, 10, "Сабарів" },
                    { 66, 10, "Старе місто" },
                    { 67, 11, "Дніпровський" },
                    { 68, 11, "Корабельний" },
                    { 69, 11, "Суворовський" },
                    { 70, 12, "Київський" },
                    { 71, 12, "Шевченківський" },
                    { 72, 12, "Подільський" },
                    { 73, 13, "Деснянський" },
                    { 74, 13, "Новозаводський" },
                    { 75, 14, "Придніпровський" },
                    { 76, 14, "Соснівський" },
                    { 77, 15, "Богунський" },
                    { 78, 15, "Корольовський" },
                    { 79, 16, "Ковпаківський" },
                    { 80, 16, "Зарічний" },
                    { 81, 17, "Фортечний" },
                    { 82, 17, "Подільський" },
                    { 83, 18, "Автозаводський" },
                    { 84, 18, "Крюківський" },
                    { 85, 19, "Дніпровський" },
                    { 86, 19, "Заводський" },
                    { 87, 19, "Південний" },
                    { 88, 20, "Першотравневий" },
                    { 89, 20, "Садгірський" },
                    { 90, 20, "Шевченківський" }
                });

            migrationBuilder.InsertData(
                table: "Masters",
                columns: new[] { "Id", "About", "Address", "CategoryId", "DistrictId", "ExperienceYears", "IsBlocked", "Latitude", "Longitude", "SlotStepMin", "Slug", "UserId" },
                values: new object[,]
                {
                    { 900001, "Тестовий майстер з Free тарифом", null, 1, 1, 3, false, null, null, 30, "master-free-1", 900001 },
                    { 900002, "Тестовий майстер з Free тарифом", null, 1, 1, 5, false, null, null, 30, "master-free-2", 900002 }
                });

            migrationBuilder.InsertData(
                table: "Services",
                columns: new[] { "Id", "Description", "DurationMin", "Included", "MasterId", "Name", "Price" },
                values: new object[,]
                {
                    { 920001, "Манікюр + гель-лак", 60, "Консультація та виконання послуги", 900001, "Манікюр + гель-лак", 300m },
                    { 920002, "Манікюр + гель", 60, "Консультація та виконання послуги", 900001, "Манікюр + гель", 250m },
                    { 920003, "Педикюр", 30, "Консультація та виконання послуги", 900001, "Педикюр", 801m }
                });

            migrationBuilder.InsertData(
                table: "Subscriptions",
                columns: new[] { "Id", "ExpiresAt", "MasterId", "Plan", "Status" },
                values: new object[,]
                {
                    { 900001, new DateTimeOffset(new DateTime(2099, 12, 31, 23, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 900001, 0, 0 },
                    { 900002, new DateTimeOffset(new DateTime(2026, 9, 24, 23, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 900002, 2, 0 }
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

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_MasterId",
                table: "Bookings",
                column: "MasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ServiceId",
                table: "Bookings",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UserId",
                table: "Bookings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DaysOff_MasterId",
                table: "DaysOff",
                column: "MasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Districts_CityId",
                table: "Districts",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_MasterId",
                table: "Favorites",
                column: "MasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_UserId_MasterId",
                table: "Favorites",
                columns: new[] { "UserId", "MasterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Masters_CategoryId",
                table: "Masters",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Masters_DistrictId",
                table: "Masters",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Masters_UserId",
                table: "Masters",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SubscriptionId",
                table: "Payments",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_Email",
                table: "PendingRegistrations",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_BookingId",
                table: "Reviews",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_MasterId",
                table: "Schedules",
                column: "MasterId");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePhotos_ServiceId",
                table: "ServicePhotos",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_MasterId",
                table: "Services",
                column: "MasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_MasterId",
                table: "Subscriptions",
                column: "MasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DaysOff");

            migrationBuilder.DropTable(
                name: "Favorites");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PendingRegistrations");

            migrationBuilder.DropTable(
                name: "PendingResets");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "Schedules");

            migrationBuilder.DropTable(
                name: "ServicePhotos");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "Masters");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Districts");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Cities");
        }
    }
}
