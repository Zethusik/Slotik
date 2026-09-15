using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models.Enums;

namespace Slotik.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Superadmin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/Admin/stats
    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> GetStats()
    {
        var now = DateTimeOffset.UtcNow;
        var currentMonthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var mastersTotal = await _context.Masters.CountAsync();
        var clientsTotal = await _context.Users.CountAsync(u => u.Role == UserRole.Client);
        var bookingsTotal = await _context.Bookings.CountAsync();
        var revenueTotal = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Success && p.PaidAt >= currentMonthStart)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var activeSubs = await _context.Subscriptions
            .CountAsync(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt >= now);

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
        var currentMonthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var prevMonthStart = currentMonthStart.AddMonths(-1);

        var currentIncome = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Success && p.PaidAt >= currentMonthStart)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var prevIncome = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Success && p.PaidAt >= prevMonthStart && p.PaidAt < currentMonthStart)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var newMastersCurrent = await _context.Masters.CountAsync();
        var newMastersPrev = 0; // for test %

        var newClientsCurrent = await _context.Users.CountAsync(u => u.Role == UserRole.Client);
        var newClientsPrev = 0;

        var paidSubs = await _context.Subscriptions
            .CountAsync(s => s.Status == SubscriptionStatus.Active && s.Plan != SubscriptionPlan.Free);

        var dailyData = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Success && p.PaidAt >= currentMonthStart)
            .GroupBy(p => p.PaidAt.Day)
            .Select(g => new DailyRevenueDto
            {
                Day = g.Key,
                Amount = g.Sum(p => p.Amount)
            })
            .OrderBy(d => d.Day)
            .ToListAsync();

        double CalcChange(decimal current, decimal prev) =>
            prev == 0 ? (current > 0 ? 100.0 : 0.0) : (double)Math.Round(((current - prev) / prev) * 100, 1);

        return Ok(new FinanceStatsDto
        {
            MonthlyIncome = currentIncome,
            MonthlyIncomeChange = CalcChange(currentIncome, prevIncome),
            NewMasters = newMastersCurrent,
            NewMastersChange = CalcChange(newMastersCurrent, newMastersPrev),
            NewClients = newClientsCurrent,
            NewClientsChange = CalcChange(newClientsCurrent, newClientsPrev),
            PaidSubscriptions = paidSubs,
            DailyRevenue = dailyData
        });
    }

    [HttpGet]
    public async Task<ActionResult> Categories()
    {
        var cats = await _context.Categories.Select(c => new
        {
            id = c.Id,
            name = c.Name,
            icon = c.Icon,
            mastersCount = _context.Masters.Count(m => m.CategoryId == c.Id),
            IsHiddenFromCatalog = c.IsHiddenFromCatalog
        }).ToListAsync();
        return Ok(cats);
    }

    [HttpPatch("{id:int}/visibility")]

    public async Task<ActionResult> ChangeVisibility(int id)
    {
        var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);

        if (cat == null) { return NotFound("Not found category."); }

        if (await _context.Masters.AnyAsync(m=>m.CategoryId == id)) 
        { 
            cat.IsHiddenFromCatalog = false;
            await _context.SaveChangesAsync();
            return BadRequest("This category have masters"); 

        }

        cat.IsHiddenFromCatalog = !cat.IsHiddenFromCatalog;
        await _context.SaveChangesAsync();

        return Ok(new {Id=id,IsHiddenFromCatalog = cat.IsHiddenFromCatalog });
    }

    // GET /api/Master/{id}
    [HttpGet("Master/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        

        var m = await _context.Masters.Where(m => m.Id == id)
            .Include(m => m.User)
            .Include(m => m.Category)
            .Include(m => m.Subscriptions)
            .Include(m => m.District)
            .ThenInclude(d => d.City)
            .FirstOrDefaultAsync(m => m.Id == id);

        var activeSubscription = m.Subscriptions
        .Where(s =>
            (s.Status == SubscriptionStatus.Active &&
             s.ExpiresAt > DateTimeOffset.UtcNow)
            || s.Plan == SubscriptionPlan.Free)
        .OrderByDescending(s => s.ExpiresAt)
        .FirstOrDefault();

        //if (activeSubscription == null) { return NotFound("not found subscription"); }

        var dto = new FullMasterDto
        {
            Id = m.Id,
            FirstName = m.User.FirstName,
            LastName = m.User.LastName,
            Category = m.Category.Name,
            City = m.District?.City?.Name ?? "Unknown",
            Status = m.Subscriptions.Any(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > DateTimeOffset.UtcNow || s.Plan == SubscriptionPlan.Free)
                ? "active"
                : "expired",
            SubscriptionUntil = m.Subscriptions.Any(s =>
                            s.Plan == SubscriptionPlan.Free)
                                ? "infinity"
                                : activeSubscription?.ExpiresAt.ToString("O"),
            Tariff = activeSubscription?
            .Plan
            .ToString()
            .ToLower() ?? "free",

            IsBlocked = m.IsBlocked,
            AvatarUrl = null,  // to do avatar url upload logic
            DistrictName = m.District.Name,
            CreatedAt = m.User.CreatedAt,
            slug = m.Slug,
            Email = m.User.Email,
            Phone = m.User.Phone,
            TariffPrice = 0,  // no pricing yet and no payments logic
            nextPaymentAt = null, // no payments logic too
            BookingsCount = 0, // no bookings logic yet






        };

        if (activeSubscription.Plan == SubscriptionPlan.Free) {
            dto.BillingPeriod = null;
            dto.SubscriptionUntil = null;
        }

        if (m == null) return NotFound(new { message = "Master not found" });

        return Ok(dto);
        

    }



}


    


