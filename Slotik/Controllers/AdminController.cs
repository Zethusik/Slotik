using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models.Enums;
using Slotik.Services;

namespace Slotik.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Superadmin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly LiqPayService _prices;

    public AdminController(AppDbContext context, LiqPayService prices)
    {
        _context = context;
        _prices = prices;
    }

    // GET /api/Admin/stats
    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> GetStats()
    {
        var now = DateTimeOffset.UtcNow;

        var currentMonthStart = new DateTimeOffset(
            now.Year,
            now.Month,
            1,
            0,
            0,
            0,
            TimeSpan.Zero
        );

        var mastersTotal = await _context.Masters.CountAsync();

        var clientsTotal = await _context.Users
            .CountAsync(u => u.Role == UserRole.Client);

        var bookingsTotal = await _context.Bookings.CountAsync();

        var revenueTotal = await _context.Payments
            .Where(p =>
                p.Status == PaymentStatus.Success &&
                p.PaidAt.HasValue &&
                p.PaidAt.Value >= currentMonthStart)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var activeSubs = await _context.Subscriptions
            .CountAsync(s =>
                s.Status == SubscriptionStatus.Active &&
                s.ExpiresAt >= now);

        return Ok(new AdminStatsDto
        {
            MastersTotal = mastersTotal,
            ClientsTotal = clientsTotal,
            BookingsTotal = bookingsTotal,
            RevenueTotal = revenueTotal,
            ActiveSubscriptions = activeSubs
        });
    }

    // GET /api/Admin/finance
    [HttpGet("finance")]
    public async Task<ActionResult<FinanceStatsDto>> GetFinanceStats()
    {
        var now = DateTimeOffset.UtcNow;

        var currentMonthStart = new DateTimeOffset(
            now.Year,
            now.Month,
            1,
            0,
            0,
            0,
            TimeSpan.Zero
        );

        var prevMonthStart = currentMonthStart.AddMonths(-1);

        var currentIncome = await _context.Payments
            .Where(p =>
                p.Status == PaymentStatus.Success &&
                p.PaidAt.HasValue &&
                p.PaidAt.Value >= currentMonthStart)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var prevIncome = await _context.Payments
            .Where(p =>
                p.Status == PaymentStatus.Success &&
                p.PaidAt.HasValue &&
                p.PaidAt.Value >= prevMonthStart &&
                p.PaidAt.Value < currentMonthStart)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var newMastersCurrent = await _context.Masters.CountAsync();

        var newMastersPrev = 0; // for test %

        var newClientsCurrent = await _context.Users
            .CountAsync(u => u.Role == UserRole.Client);

        var newClientsPrev = 0;

        var paidSubs = await _context.Subscriptions
            .CountAsync(s =>
                s.Status == SubscriptionStatus.Active &&
                s.Plan != SubscriptionPlan.Free);

        var dailyData = await _context.Payments
            .Where(p =>
                p.Status == PaymentStatus.Success &&
                p.PaidAt.HasValue &&
                p.PaidAt.Value >= currentMonthStart)
            .GroupBy(p => p.PaidAt!.Value.Day)
            .Select(g => new DailyRevenueDto
            {
                Day = g.Key,
                Amount = g.Sum(p => p.Amount)
            })
            .OrderBy(d => d.Day)
            .ToListAsync();

        double CalcChange(decimal current, decimal prev) =>
            prev == 0
                ? (current > 0 ? 100.0 : 0.0)
                : (double)Math.Round(
                    ((current - prev) / prev) * 100,
                    1
                );

        return Ok(new FinanceStatsDto
        {
            MonthlyIncome = currentIncome,
            MonthlyIncomeChange = CalcChange(
                currentIncome,
                prevIncome
            ),

            NewMasters = newMastersCurrent,
            NewMastersChange = CalcChange(
                newMastersCurrent,
                newMastersPrev
            ),

            NewClients = newClientsCurrent,
            NewClientsChange = CalcChange(
                newClientsCurrent,
                newClientsPrev
            ),

            PaidSubscriptions = paidSubs,
            DailyRevenue = dailyData
        });
    }

    // GET /api/Admin
    [HttpGet]
    public async Task<ActionResult> Categories()
    {
        var cats = await _context.Categories
            .Select(c => new
            {
                id = c.Id,
                name = c.Name,
                icon = c.Icon,

                mastersCount = _context.Masters
                    .Count(m => m.CategoryId == c.Id),

                IsHiddenFromCatalog = c.IsHiddenFromCatalog
            })
            .ToListAsync();

        return Ok(cats);
    }

    // PATCH /api/Admin/{id}/visibility
    [HttpPatch("{id:int}/visibility")]
    public async Task<ActionResult> ChangeVisibility(int id)
    {
        var cat = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cat == null)
        {
            return NotFound("Not found category.");
        }

        if (await _context.Masters.AnyAsync(
                m => m.CategoryId == id))
        {
            cat.IsHiddenFromCatalog = false;

            await _context.SaveChangesAsync();

            return BadRequest(
                "This category have masters"
            );
        }

        cat.IsHiddenFromCatalog =
            !cat.IsHiddenFromCatalog;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            Id = id,
            IsHiddenFromCatalog =
                cat.IsHiddenFromCatalog
        });
    }

    // GET /api/Admin/Master/{id}
    [HttpGet("Master/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var master = await _context.Masters
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.Category)
            .Include(m => m.Subscriptions)
            .Include(m => m.District)
                .ThenInclude(d => d.City)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (master == null)
        {
            return NotFound(new
            {
                message = "Master not found"
            });
        }

        var activeSubscription = EffectivePlanResolver.Resolve(
            master.Subscriptions,
            DateTimeOffset.UtcNow);

        var tariffPrice = 0m;
        if (activeSubscription != null && activeSubscription.Plan != SubscriptionPlan.Free)
        {
            tariffPrice = _prices.GetPrice(activeSubscription.Plan);
        }

        var payments = await _context.Payments
            .AsNoTracking()
            .Where(payment => payment.Subscription.MasterId == id)
            .OrderByDescending(payment => payment.PaidAt ?? payment.CreatedAt)
            .ThenByDescending(payment => payment.Id)
            .Select(payment => new AdminPaymentDto
            {
                Id = payment.Id,
                CreatedAt = payment.CreatedAt,
                PaidAt = payment.PaidAt,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Status = payment.Status,
                ProviderStatus = payment.ProviderStatus
            })
            .ToListAsync();

        var bookingsCount = await _context.Bookings
            .CountAsync(booking => booking.MasterId == id);

        var isBlocked = master.IsBlocked;

        var currentStatus =
            isBlocked
                ? "blocked"
                : "active";

        var currentTariff =
            activeSubscription != null
                ? activeSubscription.Plan
                    .ToString()
                    .ToLower()
                : "none";

        var dto = new FullMasterDto
        {
            Id = master.Id,

            FirstName = master.User.FirstName,
            LastName = master.User.LastName,

            Category = master.Category.Name,

            City =
                master.District?.City?.Name
                ?? "Unknown",

            Status = currentStatus,

            SubscriptionUntil =
                activeSubscription != null && activeSubscription.Plan != SubscriptionPlan.Free
                    ? activeSubscription
                        .ExpiresAt
                        .ToString("O")
                    : null,

            Tariff = currentTariff,

            IsBlocked = master.IsBlocked,

            AvatarUrl = string.IsNullOrWhiteSpace(master.User.AvatarUrl)
                ? null
                : master.User.AvatarUrl,
            

            DistrictName =
                master.District?.Name
                ?? "Unknown",

            CreatedAt = master.User.CreatedAt,

            slug = master.Slug,

            Email = master.User.Email,

            Phone = master.User.Phone,

            TariffPrice = tariffPrice,

            nextPaymentAt = null,
            

            BookingsCount = bookingsCount,
            Payments = payments
        };

        if (activeSubscription == null || activeSubscription.Plan == SubscriptionPlan.Free)
        {
            dto.BillingPeriod = null;
            dto.SubscriptionUntil = null;
        }

        return Ok(dto);
    }
}
