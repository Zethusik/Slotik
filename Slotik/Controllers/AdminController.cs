using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models.Enums;

namespace Slotik.Controllers
{
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

        [HttpGet("stats")]
        public async Task<ActionResult<AdminStatsDto>> GetStats()
        {
            var now = DateTimeOffset.UtcNow;

            var mastersTotal = await _context.Masters.CountAsync();
            var activeSubs = await _context.Subscriptions
                .CountAsync(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt >= now);
            var bookingsTotal = await _context.Bookings.CountAsync();

            var monthlyIncome = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Success)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            return Ok(new AdminStatsDto
            {
                MastersTotal = mastersTotal,
                ActiveSubscriptions = activeSubs,
                MonthlyIncome = monthlyIncome,
                BookingsTotal = bookingsTotal
            });
        }

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
            var newClientsCurrent = await _context.Users.CountAsync(u => u.Role == UserRole.Client);

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
                prev == 0 ? (current > 0 ? 100 : 0) : (double)Math.Round(((current - prev) / prev) * 100, 1);

            return Ok(new FinanceStatsDto
            {
                MonthlyIncome = currentIncome,
                MonthlyIncomeChange = CalcChange(currentIncome, prevIncome),
                NewMasters = newMastersCurrent,
                NewMastersChange = 0,
                NewClients = newClientsCurrent,
                NewClientsChange = 0,
                PaidSubscriptions = paidSubs,
                DailyRevenue = dailyData
            });
        }
    }
}