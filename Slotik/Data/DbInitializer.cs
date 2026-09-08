using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Slotik.Models;
using Slotik.Models.Enums;

namespace Slotik.Data
{
    public static class DbInitializer
    {
        public static async Task SeedDataAsync(AppDbContext context)
        {
            if (!await context.Users.AnyAsync(u => u.Role == UserRole.Superadmin))
            {
                var admin = new User
                {
                    FirstName = "Super",
                    LastName = "Admin",
                    Phone = "+380000000000",
                    Email = "superadmin@slotik.local",
                    // SuperAdmin123!
                    PasswordHash = "d357150517d3e65ae84985f7b705ad99fdc38372a22ecea0cecaf8aaf820a249",
                    Role = UserRole.Superadmin
                };
                context.Users.Add(admin);
                await context.SaveChangesAsync();
            }

            if (await context.Masters.AnyAsync()) return;

            var district = await context.Districts.FirstOrDefaultAsync(d => d.Id == 6)
                           ?? await context.Districts.FirstOrDefaultAsync();

            if (district == null) return;

            var cat1 = new Category { Name = "Манікюр", Icon = "hand-finger" };
            var cat2 = new Category { Name = "Перукар", Icon = "scissors" };
            var cat3 = new Category { Name = "Візаж", Icon = "sparkles" };
            var cat4 = new Category { Name = "Брови та вії", Icon = "eye" };
            var cat5 = new Category { Name = "Тату", Icon = "needle" };

            context.Categories.AddRange(cat1, cat2, cat3, cat4, cat5);
            await context.SaveChangesAsync();

            var categories = new[] { cat1, cat2, cat3, cat4, cat5 };

            for (int i = 1; i <= 10; i++)
            {
                var user = new User
                {
                    FirstName = "Майстер",
                    LastName = $"{i}",
                    Email = $"master{i}@slotik.com",
                    PasswordHash = "hashed_password",
                    Role = UserRole.Master
                };
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var master = new Master
                {
                    UserId = user.Id,
                    CategoryId = categories[i % categories.Length].Id,
                    DistrictId = district.Id,
                    Slug = $"master-{i}",
                    ExperienceYears = 2 + (i % 5),
                    SlotStepMin = 30
                };
                context.Masters.Add(master);
                await context.SaveChangesAsync();

                var sub = new Subscription
                {
                    MasterId = master.Id,
                    Plan = i % 2 == 0 ? SubscriptionPlan.Pro : SubscriptionPlan.Basic,
                    Status = SubscriptionStatus.Active,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(15)
                };
                context.Subscriptions.Add(sub);
                await context.SaveChangesAsync();

                context.Payments.Add(new Payment
                {
                    SubscriptionId = sub.Id,
                    Amount = i % 2 == 0 ? 200m : 150m,
                    Status = PaymentStatus.Success,
                    PaidAt = DateTimeOffset.UtcNow.AddDays(-i)
                });
            }

            for (int i = 1; i <= 15; i++)
            {
                context.Users.Add(new User
                {
                    FirstName = "Клієнт",
                    LastName = $"{i}",
                    Email = $"client{i}@gmail.com",
                    PasswordHash = "hashed_password",
                    Role = UserRole.Client
                });
            }

            await context.SaveChangesAsync();
        }
    }
}