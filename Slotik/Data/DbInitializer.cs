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
        public static async Task SeedDataAsync(AppDbContext context, IHostEnvironment environment)
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Demo seeding is allowed only in Development.");
            if (!await context.Users.AnyAsync(u => u.Role == UserRole.Superadmin))
            {
                var admin = new User
                {
                    FirstName = "Super",
                    LastName = "Admin",
                    Phone = "+380000000000",
                    Email = "superadmin@slotik.local",
                    PasswordHash = "d357150517d3e65ae84985f7b705ad99fdc38372a22ecea0cecaf8aaf820a249",
                    Role = UserRole.Superadmin
                };
                context.Users.Add(admin);
                await context.SaveChangesAsync();
            }

            var cityKyiv = await context.Cities.FirstOrDefaultAsync(c => c.Name == "Київ");
            if (cityKyiv == null)
            {
                cityKyiv = new City { Name = "Київ" };
                context.Cities.Add(cityKyiv);
                await context.SaveChangesAsync();
            }

            var cityLviv = await context.Cities.FirstOrDefaultAsync(c => c.Name == "Львів");
            if (cityLviv == null)
            {
                cityLviv = new City { Name = "Львів" };
                context.Cities.Add(cityLviv);
                await context.SaveChangesAsync();
            }

            var districtKyivPechersk = await context.Districts.FirstOrDefaultAsync(d => d.CityId == cityKyiv.Id && d.Name == "Печерський")
                ?? await context.Districts.FirstOrDefaultAsync(d => d.CityId == cityKyiv.Id);
            if (districtKyivPechersk == null)
            {
                districtKyivPechersk = new District { Name = "Печерський", CityId = cityKyiv.Id };
                context.Districts.Add(districtKyivPechersk);
                await context.SaveChangesAsync();
            }

            var districtKyivShevchenko = await context.Districts.FirstOrDefaultAsync(d => d.CityId == cityKyiv.Id && d.Name == "Шевченківський");
            if (districtKyivShevchenko == null)
            {
                districtKyivShevchenko = new District { Name = "Шевченківський", CityId = cityKyiv.Id };
                context.Districts.Add(districtKyivShevchenko);
                await context.SaveChangesAsync();
            }

            var districtLviv = await context.Districts.FirstOrDefaultAsync(d => d.CityId == cityLviv.Id);
            if (districtLviv == null)
            {
                districtLviv = new District { Name = "Галицький", CityId = cityLviv.Id };
                context.Districts.Add(districtLviv);
                await context.SaveChangesAsync();
            }

            var cat1 = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Манікюр");
            if (cat1 == null)
            {
                cat1 = new Category { Name = "Манікюр", Icon = "hand-finger" };
                context.Categories.Add(cat1);
            }

            var cat2 = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Перукар");
            if (cat2 == null)
            {
                cat2 = new Category { Name = "Перукар", Icon = "scissors" };
                context.Categories.Add(cat2);
            }

            var cat3 = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Візаж");
            if (cat3 == null)
            {
                cat3 = new Category { Name = "Візаж", Icon = "sparkles" };
                context.Categories.Add(cat3);
            }

            var cat4 = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Брови та вії");
            if (cat4 == null)
            {
                cat4 = new Category { Name = "Брови та вії", Icon = "eye" };
                context.Categories.Add(cat4);
            }

            var cat5 = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Тату");
            if (cat5 == null)
            {
                cat5 = new Category { Name = "Тату", Icon = "needle" };
                context.Categories.Add(cat5);
            }
            await context.SaveChangesAsync();

            var categories = new[] { cat1, cat2, cat3, cat4, cat5 };

            // 1. Киевские мастера
            for (int i = 1; i <= 6; i++)
            {
                var email = $"master_kyiv_{i}@slotik.com";
                if (!await context.Users.AnyAsync(u => u.Email == email))
                {
                    var user = new User
                    {
                        FirstName = "Олена",
                        LastName = $"Київська {i}",
                        Email = email,
                        Phone = $"+38050111220{i}",
                        PasswordHash = "d357150517d3e65ae84985f7b705ad99fdc38372a22ecea0cecaf8aaf820a249",
                        Role = UserRole.Master
                    };
                    context.Users.Add(user);
                    await context.SaveChangesAsync();

                    var assignedDistrict = (i <= 3) ? districtKyivPechersk : districtKyivShevchenko;

                    var master = new Master
                    {
                        UserId = user.Id,
                        CategoryId = categories[i % categories.Length].Id,
                        DistrictId = assignedDistrict.Id,
                        Slug = $"master-kyiv-{i}",
                        ExperienceYears = 2 + (i % 5),
                        SlotStepMin = 30
                    };
                    context.Masters.Add(master);
                    await context.SaveChangesAsync();

                    if (i == 1)
                    {
                        context.Services.AddRange(
                            new Service
                            {
                                MasterId = master.Id,
                                Name = "Манікюр + гель-лак",
                                Price = 600m,
                                DurationMin = 90,
                                Description = "Зняття, комбінований манікюр, покриття гель-лаком",
                                Included = "Манікюр, вирівнювання, дизайн 2 пальців"
                            },
                            new Service
                            {
                                MasterId = master.Id,
                                Name = "Манікюр + гель",
                                Price = 800m,
                                DurationMin = 120,
                                Description = "Укріплення та моделювання твердим гелем",
                                Included = "Манікюр, укріплення, однотонне покриття"
                            },
                            new Service
                            {
                                MasterId = master.Id,
                                Name = "Педикюр",
                                Price = 750m,
                                DurationMin = 90,
                                Description = "Апаратний педикюр з обробкою стопи та пальчиків",
                                Included = "Стопа, пальчики, крем"
                            }
                        );
                    }
                    else
                    {
                        context.Services.Add(new Service
                        {
                            MasterId = master.Id,
                            Name = i % 2 == 0 ? "Класичний манікюр" : "Стрижка та укладка",
                            Price = 500m,
                            DurationMin = 60
                        });
                    }

                    context.Subscriptions.Add(new Subscription
                    {
                        MasterId = master.Id,
                        Plan = SubscriptionPlan.Pro,
                        Status = SubscriptionStatus.Active,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(15)
                    });
                    await context.SaveChangesAsync();
                }
            }

            // 2. Львовские мастера
            for (int i = 1; i <= 4; i++)
            {
                var email = $"master_lviv_{i}@slotik.com";
                if (!await context.Users.AnyAsync(u => u.Email == email))
                {
                    var user = new User
                    {
                        FirstName = "Ірина",
                        LastName = $"Львівська {i}",
                        Email = email,
                        Phone = $"+38050222330{i}",
                        PasswordHash = "d357150517d3e65ae84985f7b705ad99fdc38372a22ecea0cecaf8aaf820a249",
                        Role = UserRole.Master
                    };
                    context.Users.Add(user);
                    await context.SaveChangesAsync();

                    var master = new Master
                    {
                        UserId = user.Id,
                        CategoryId = categories[(i + 1) % categories.Length].Id,
                        DistrictId = districtLviv.Id,
                        Slug = $"master-lviv-{i}",
                        ExperienceYears = 3 + i,
                        SlotStepMin = 30
                    };
                    context.Masters.Add(master);
                    await context.SaveChangesAsync();

                    context.Services.Add(new Service
                    {
                        MasterId = master.Id,
                        Name = "Апаратний манікюр",
                        Price = 650m,
                        DurationMin = 90
                    });

                    context.Subscriptions.Add(new Subscription
                    {
                        MasterId = master.Id,
                        Plan = SubscriptionPlan.Basic,
                        Status = SubscriptionStatus.Active,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(20)
                    });
                    await context.SaveChangesAsync();
                }
            }

            // 3. Мастер на 27 часов
            if (!await context.Users.AnyAsync(u => u.Email == "master27h@slotik.com"))
            {
                var user27 = new User
                {
                    FirstName = "Анна",
                    LastName = "27Годин",
                    Email = "master27h@slotik.com",
                    Phone = "+380503334455",
                    PasswordHash = "d357150517d3e65ae84985f7b705ad99fdc38372a22ecea0cecaf8aaf820a249",
                    Role = UserRole.Master
                };
                context.Users.Add(user27);
                await context.SaveChangesAsync();

                var master27 = new Master
                {
                    UserId = user27.Id,
                    CategoryId = cat1.Id,
                    DistrictId = districtKyivPechersk.Id,
                    Slug = "master-27hours",
                    ExperienceYears = 3,
                    SlotStepMin = 30
                };
                context.Masters.Add(master27);
                await context.SaveChangesAsync();

                context.Subscriptions.Add(new Subscription
                {
                    MasterId = master27.Id,
                    Plan = SubscriptionPlan.Pro,
                    Status = SubscriptionStatus.Active,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(27)
                });
                await context.SaveChangesAsync();
            }

            // 4. Мастер с истекшей подпиской (переход на Free)
            if (!await context.Users.AnyAsync(u => u.Email == "masterexpired@slotik.com"))
            {
                var userExpired = new User
                {
                    FirstName = "Марія",
                    LastName = "БезПідписки",
                    Email = "masterexpired@slotik.com",
                    Phone = "+380504445566",
                    PasswordHash = "d357150517d3e65ae84985f7b705ad99fdc38372a22ecea0cecaf8aaf820a249",
                    Role = UserRole.Master
                };
                context.Users.Add(userExpired);
                await context.SaveChangesAsync();

                var masterExpired = new Master
                {
                    UserId = userExpired.Id,
                    CategoryId = cat2.Id,
                    DistrictId = districtLviv.Id,
                    Slug = "master-expired-to-free",
                    ExperienceYears = 4,
                    SlotStepMin = 30
                };
                context.Masters.Add(masterExpired);
                await context.SaveChangesAsync();

                context.Subscriptions.Add(new Subscription
                {
                    MasterId = masterExpired.Id,
                    Plan = SubscriptionPlan.Basic,
                    Status = SubscriptionStatus.Active,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(-2)
                });
                await context.SaveChangesAsync();
            }


            // =====================================================
            //  TEST MASTERS: Masterfortest1 ... Masterfortest20
            // =====================================================

            const string testMasterPasswordHash =
                "d357150517d3e65ae84985f7b705ad99fdc38372a22ecea0cecaf8aaf820a249";

            for (int i = 1; i <= 20; i++)
            {
                var email = $"masterfortest{i}@slotik.com";

                if (await context.Users.AnyAsync(u => u.Email == email))
                    continue;

                var user = new User
                {
                    FirstName = "Masterfortest",
                    LastName = i.ToString(),
                    Email = email,
                    Phone = $"+38099000{i:D3}",
                    PasswordHash = testMasterPasswordHash,
                    Role = UserRole.Master
                };

                context.Users.Add(user);
                await context.SaveChangesAsync();

                var master = new Master
                {
                    UserId = user.Id,
                    CategoryId = cat1.Id,
                    DistrictId = districtKyivPechersk.Id,
                    Slug = $"masterfortest-{i}",
                    About = $"Тестовий майстер №{i}",
                    ExperienceYears = 1 + (i % 5),
                    SlotStepMin = 30,
                    IsBlocked = false
                };

                context.Masters.Add(master);
                await context.SaveChangesAsync();


                if (i >= 1 && i <= 5)
                {
                    context.Subscriptions.Add(new Subscription
                    {
                        MasterId = master.Id,
                        Plan = SubscriptionPlan.Free,
                        Status = SubscriptionStatus.Active,
                        ExpiresAt = DateTimeOffset.MaxValue
                    });
                }



                else if (i >= 11 && i <= 13)
                {
                    context.Subscriptions.Add(new Subscription
                    {
                        MasterId = master.Id,
                        Plan = SubscriptionPlan.Basic,
                        Status = SubscriptionStatus.Active,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
                    });
                }


                else if (i >= 14 && i <= 16)
                {
                    context.Subscriptions.Add(new Subscription
                    {
                        MasterId = master.Id,
                        Plan = SubscriptionPlan.Pro,
                        Status = SubscriptionStatus.Active,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
                    });
                }

                else if (i >= 17 && i <= 18)
                {
                    context.Subscriptions.Add(new Subscription
                    {
                        MasterId = master.Id,
                        Plan = i == 17
                            ? SubscriptionPlan.Basic
                            : SubscriptionPlan.Pro,

                        Status = SubscriptionStatus.Active,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(-7)
                    });
                }

                await context.SaveChangesAsync();
            }

            await context.SaveChangesAsync();
        }
    }
}
