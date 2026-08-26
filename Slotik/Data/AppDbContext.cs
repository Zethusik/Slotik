using Microsoft.EntityFrameworkCore;
using Slotik.Models;
using Slotik.Models.Enums;

namespace Slotik.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Master> Masters { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<District> Districts { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<ServicePhoto> ServicePhotos { get; set; }
    public DbSet<Schedule> Schedules { get; set; }
    public DbSet<DaysOff> DaysOff { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Favorite> Favorites { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<Payment> Payments { get; set; }

    public DbSet<PendingRegistration> PendingRegistrations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PendingRegistration>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<User>().HasData(
           new User
           {
               Id = -1,
               Name = "Super Admin",
               Phone = "+380000000000",
               Email = "superadmin@slotik.local",

               // Password: SuperAdmin123!
               PasswordHash = "d357150517d3e65ae84985f7b705ad99fdc38372a22ecea0cecaf8aaf820a249",

               Role = UserRole.Superadmin,
               TelegramChatId = null
           }
       );

        modelBuilder.Entity<Review>()
            .HasIndex(r => r.BookingId)
            .IsUnique();

        modelBuilder.Entity<Favorite>()
            .HasIndex(f => new { f.UserId, f.MasterId })
            .IsUnique();

        modelBuilder.Entity<Master>()
            .HasOne(m => m.District)
            .WithMany()
            .HasForeignKey(m => m.DistrictId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Subscription>()
            .HasOne(s => s.Master)
            .WithMany(m => m.Subscriptions)
            .HasForeignKey(s => s.MasterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Subscription)
            .WithMany(s => s.Payments)
            .HasForeignKey(p => p.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // ==========================================
        //         МІСТА ТА РАЙОНІВ УКРАЇНИ
        // ==========================================

        modelBuilder.Entity<City>().HasData(
            new City { Id = 1, Name = "Київ" },
            new City { Id = 2, Name = "Харків" },
            new City { Id = 3, Name = "Одеса" },
            new City { Id = 4, Name = "Дніпро" },
            new City { Id = 5, Name = "Львів" },
            new City { Id = 6, Name = "Запоріжжя" },
            new City { Id = 7, Name = "Кривий Ріг" },
            new City { Id = 8, Name = "Миколаїв" },
            new City { Id = 9, Name = "Маріуполь" },
            new City { Id = 10, Name = "Вінниця" },
            new City { Id = 11, Name = "Херсон" },
            new City { Id = 12, Name = "Полтава" },
            new City { Id = 13, Name = "Чернігів" },
            new City { Id = 14, Name = "Черкаси" },
            new City { Id = 15, Name = "Житомир" },
            new City { Id = 16, Name = "Суми" },
            new City { Id = 17, Name = "Кропивницький" },
            new City { Id = 18, Name = "Кременчук" },
            new City { Id = 19, Name = "Кам'янське" },
            new City { Id = 20, Name = "Чернівці" }
        );

        modelBuilder.Entity<District>().HasData(
            // 1. Київ
            new District { Id = 1, CityId = 1, Name = "Голосіївський" },
            new District { Id = 2, CityId = 1, Name = "Дарницький" },
            new District { Id = 3, CityId = 1, Name = "Деснянський" },
            new District { Id = 4, CityId = 1, Name = "Дніпровський" },
            new District { Id = 5, CityId = 1, Name = "Оболонський" },
            new District { Id = 6, CityId = 1, Name = "Печерський" },
            new District { Id = 7, CityId = 1, Name = "Подільський" },
            new District { Id = 8, CityId = 1, Name = "Святошинський" },
            new District { Id = 9, CityId = 1, Name = "Солом'янський" },
            new District { Id = 10, CityId = 1, Name = "Шевченківський" },

            // 2. Харків
            new District { Id = 11, CityId = 2, Name = "Шевченківський" },
            new District { Id = 12, CityId = 2, Name = "Київський" },
            new District { Id = 13, CityId = 2, Name = "Салтівський" },
            new District { Id = 14, CityId = 2, Name = "Немішлянський" },
            new District { Id = 15, CityId = 2, Name = "Індустріальний" },
            new District { Id = 16, CityId = 2, Name = "Слобідський" },
            new District { Id = 17, CityId = 2, Name = "Основ'янський" },
            new District { Id = 18, CityId = 2, Name = "Новобаварський" },
            new District { Id = 19, CityId = 2, Name = "Холодногірський" },

            // 3. Одеса
            new District { Id = 20, CityId = 3, Name = "Київський" },
            new District { Id = 21, CityId = 3, Name = "Пересипський" },
            new District { Id = 22, CityId = 3, Name = "Приморський" },
            new District { Id = 23, CityId = 3, Name = "Хаджибейський" },

            // 4. Дніпро
            new District { Id = 24, CityId = 4, Name = "Амур-Нижньодніпровський" },
            new District { Id = 25, CityId = 4, Name = "Індустріальний" },
            new District { Id = 26, CityId = 4, Name = "Новокодацький" },
            new District { Id = 27, CityId = 4, Name = "Самарський" },
            new District { Id = 28, CityId = 4, Name = "Соборний" },
            new District { Id = 29, CityId = 4, Name = "Центральний" },
            new District { Id = 30, CityId = 4, Name = "Чечелівський" },
            new District { Id = 31, CityId = 4, Name = "Шевченківський" },

            // 5. Львів
            new District { Id = 32, CityId = 5, Name = "Галицький" },
            new District { Id = 33, CityId = 5, Name = "Залізничний" },
            new District { Id = 34, CityId = 5, Name = "Личаківський" },
            new District { Id = 35, CityId = 5, Name = "Сихівський" },
            new District { Id = 36, CityId = 5, Name = "Франківський" },
            new District { Id = 37, CityId = 5, Name = "Шевченківський" },

            // 6. Запоріжжя
            new District { Id = 38, CityId = 6, Name = "Олександрівський" },
            new District { Id = 39, CityId = 6, Name = "Заводський" },
            new District { Id = 40, CityId = 6, Name = "Комунарський" },
            new District { Id = 41, CityId = 6, Name = "Дніпровський" },
            new District { Id = 42, CityId = 6, Name = "Вознесенівський" },
            new District { Id = 43, CityId = 6, Name = "Хортицький" },
            new District { Id = 44, CityId = 6, Name = "Шевченківський" },

            // 7. Кривий Ріг
            new District { Id = 45, CityId = 7, Name = "Довгинцівський" },
            new District { Id = 46, CityId = 7, Name = "Покровський" },
            new District { Id = 47, CityId = 7, Name = "Інгулецький" },
            new District { Id = 48, CityId = 7, Name = "Металургійний" },
            new District { Id = 49, CityId = 7, Name = "Нікопольський" },
            new District { Id = 50, CityId = 7, Name = "Саксаганський" },
            new District { Id = 51, CityId = 7, Name = "Тернівський" },
            new District { Id = 52, CityId = 7, Name = "Центрально-Міський" },

            // 8. Миколаїв
            new District { Id = 53, CityId = 8, Name = "Заводський" },
            new District { Id = 54, CityId = 8, Name = "Корабельний" },
            new District { Id = 55, CityId = 8, Name = "Інгульський" },
            new District { Id = 56, CityId = 8, Name = "Центральний" },

            // 9. Маріуполь
            new District { Id = 57, CityId = 9, Name = "Кальміуський" },
            new District { Id = 58, CityId = 9, Name = "Лівобережний" },
            new District { Id = 59, CityId = 9, Name = "Приморський" },
            new District { Id = 60, CityId = 9, Name = "Центральний" },

            // 10. Вінниця
            new District { Id = 61, CityId = 10, Name = "Замостянський" },
            new District { Id = 62, CityId = 10, Name = "Вишенька" },
            new District { Id = 63, CityId = 10, Name = "Центр" },
            new District { Id = 64, CityId = 10, Name = "П'ятничани" },
            new District { Id = 65, CityId = 10, Name = "Сабарів" },
            new District { Id = 66, CityId = 10, Name = "Старе місто" },

            // 11. Херсон
            new District { Id = 67, CityId = 11, Name = "Дніпровський" },
            new District { Id = 68, CityId = 11, Name = "Корабельний" },
            new District { Id = 69, CityId = 11, Name = "Суворовський" },

            // 12. Полтава
            new District { Id = 70, CityId = 12, Name = "Київський" },
            new District { Id = 71, CityId = 12, Name = "Шевченківський" },
            new District { Id = 72, CityId = 12, Name = "Подільський" },

            // 13. Чернігів
            new District { Id = 73, CityId = 13, Name = "Деснянський" },
            new District { Id = 74, CityId = 13, Name = "Новозаводський" },

            // 14. Черкаси
            new District { Id = 75, CityId = 14, Name = "Придніпровський" },
            new District { Id = 76, CityId = 14, Name = "Соснівський" },

            // 15. Житомир
            new District { Id = 77, CityId = 15, Name = "Богунський" },
            new District { Id = 78, CityId = 15, Name = "Корольовський" },

            // 16. Суми
            new District { Id = 79, CityId = 16, Name = "Ковпаківський" },
            new District { Id = 80, CityId = 16, Name = "Зарічний" },

            // 17. Кропивницький
            new District { Id = 81, CityId = 17, Name = "Фортечний" },
            new District { Id = 82, CityId = 17, Name = "Подільський" },

            // 18. Кременчук
            new District { Id = 83, CityId = 18, Name = "Автозаводський" },
            new District { Id = 84, CityId = 18, Name = "Крюківський" },

            // 19. Кам'янське
            new District { Id = 85, CityId = 19, Name = "Дніпровський" },
            new District { Id = 86, CityId = 19, Name = "Заводський" },
            new District { Id = 87, CityId = 19, Name = "Південний" },

            // 20. Чернівці
            new District { Id = 88, CityId = 20, Name = "Першотравневий" },
            new District { Id = 89, CityId = 20, Name = "Садгірський" },
            new District { Id = 90, CityId = 20, Name = "Шевченківський" }
        );
    }
}