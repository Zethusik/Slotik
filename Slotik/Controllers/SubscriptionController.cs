using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Models.Enums;
using Slotik.Services;

namespace Slotik.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SubscriptionController : ControllerBase
{
    private readonly AppDbContext _context;

    private const int ProTrialDays = 7;

    private readonly LiqPayService _liqPay;

    public SubscriptionController(
        AppDbContext context, LiqPayService liqPay)
    {
        _context = context;
        _liqPay = liqPay;
    }


    [HttpPost("free")]
    [Authorize(Roles = "Master")]

    public async Task<ActionResult> AssignFree() 
    {
        var userid = User.FindFirstValue("userId");

        if (!int.TryParse(userid, out var UserId))
            return Unauthorized();

        var master = await _context.Masters.Include(m=>m.Subscriptions).FirstOrDefaultAsync(m => m.UserId == UserId);
        if (master == null) { return NotFound(new { message = "Master profile not found." }); }

        var activeSub = master.Subscriptions
                .Where(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > DateTimeOffset.UtcNow && s.Plan != SubscriptionPlan.Free)
                .OrderByDescending(s => s.ExpiresAt)
                .FirstOrDefault();

        var freeplan = master.Subscriptions.FirstOrDefault(s => s.Plan == SubscriptionPlan.Free);

        if (activeSub != null || freeplan != null) { return BadRequest(new { message = "You alredy have an active subscription" }); }

        Subscription sub = new Subscription {
        Plan= SubscriptionPlan.Free,
        ExpiresAt = DateTimeOffset.MaxValue,
        Status = SubscriptionStatus.Active,
        MasterId = master.Id,
        };

        await _context.Subscriptions.AddAsync(sub);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Free plan assigned successfuly."});


    }

    // ================================
    // GET SUBSCRIPTIONS
    // ================================

    [HttpGet]
    [Authorize(Roles = "Master,Superadmin")]
    public async Task<ActionResult> GetAll()
    {
        if (User.IsInRole("Superadmin"))
        {
            var allSubscriptions =
                await _context.Subscriptions
                    .AsNoTracking()
                    .Include(s => s.Payments)
                    .Include(s => s.Master)
                    .ToListAsync();

            return Ok(allSubscriptions);
        }

        var email = GetCurrentEmail();

        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized();

        var subscriptions =
            await _context.Subscriptions
                .AsNoTracking()
                .Include(s => s.Payments)
                .Include(s => s.Master)
                    .ThenInclude(m => m.User)
                .Where(s =>
                    s.Master.User.Email == email)
                .ToListAsync();

        return Ok(subscriptions);
    }

    // ================================
    // GET SUBSCRIPTION BY ID
    // ================================

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Master,Superadmin")]
    public async Task<ActionResult> GetById(
        int id)
    {
        var sub =
            await _context.Subscriptions
                .AsNoTracking()
                .Include(s => s.Payments)
                .Include(s => s.Master)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(
                    s => s.Id == id);

        if (sub == null)
        {
            return NotFound(
                "Subscription Not Found.");
        }

        if (User.IsInRole("Superadmin"))
            return Ok(sub);

        var email = GetCurrentEmail();

        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized();

        if (!string.Equals(
                sub.Master.User.Email,
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        DateTimeOffset? nextpayment = sub.ExpiresAt;
        decimal price = 0;
        if (sub.Plan == SubscriptionPlan.Free) { nextpayment = null; }
        if (sub.Plan != SubscriptionPlan.Free) { price = _liqPay.GetPrice(sub.Plan); }

        return Ok(new { Subscription = sub,billingPeriod="month",NextPaymentAt = nextpayment,nextPaymentAmount=price,Price=price,currency="UAH"});
    }

    // ================================
    // PRO TRIAL AVAILABILITY
    // ================================

    [HttpGet("pro-trial/availability")]
    [Authorize(Roles = "Master")]
    public async Task<ActionResult>
        GetProTrialAvailability(
            CancellationToken cancellationToken)
    {
        var email = GetCurrentEmail();

        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized();

        var master =
            await _context.Masters
                .AsNoTracking()
                .Include(m => m.User)
                .FirstOrDefaultAsync(
                    m => m.User.Email == email,
                    cancellationToken);

        if (master == null)
        {
            return NotFound(
                "Master not found.");
        }

        if (master.ProTrialUsedAt != null)
        {
            return Ok(new
            {
                canStartProTrial = false,
                reason = "trial_already_used",
                proTrialUsedAt =
                    master.ProTrialUsedAt
            });
        }

        var now =
            DateTimeOffset.UtcNow;

        var hasActivePro =
            await _context.Subscriptions
                .AsNoTracking()
                .AnyAsync(
                    s =>
                        s.MasterId == master.Id &&
                        s.Plan ==
                            SubscriptionPlan.Pro &&
                        s.Status ==
                            SubscriptionStatus.Active &&
                        s.ExpiresAt > now,
                    cancellationToken);

        if (hasActivePro)
        {
            return Ok(new
            {
                canStartProTrial = false,
                reason = "active_pro_exists"
            });
        }

        return Ok(new
        {
            canStartProTrial = true,
            trialDays = ProTrialDays
        });
    }

    // ================================
    // START FREE PRO TRIAL - 7 DAYS
    // ================================

    [HttpPost("pro-trial")]
    [Authorize(Roles = "Master")]
    public async Task<ActionResult> StartProTrial(
        CancellationToken cancellationToken)
    {
        var email = GetCurrentEmail();

        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized();

        var masterId =
            await _context.Masters
                .Where(m =>
                    m.User.Email == email)
                .Select(m =>
                    (int?)m.Id)
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (masterId == null)
        {
            return NotFound(
                "Master not found.");
        }

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync(
                    cancellationToken);

        await _context.Database
            .ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({masterId.Value})",
                cancellationToken);

        var master =
            await _context.Masters
                .FirstOrDefaultAsync(
                    m => m.Id == masterId.Value,
                    cancellationToken);

        if (master == null)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            return NotFound(
                "Master not found.");
        }

        // Trial can only used
        // once per acc
        if (master.ProTrialUsedAt != null)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            return Conflict(new
            {
                error = "trial_already_used",
                message =
                    "Pro trial has already been used."
            });
        }

        var now =
            DateTimeOffset.UtcNow;

        var hasActivePro =
            await _context.Subscriptions
                .AnyAsync(
                    s =>
                        s.MasterId == master.Id &&
                        s.Plan ==
                            SubscriptionPlan.Pro &&
                        s.Status ==
                            SubscriptionStatus.Active &&
                        s.ExpiresAt > now,
                    cancellationToken);

        if (hasActivePro)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            return Conflict(new
            {
                error = "active_pro_exists",
                message =
                    "Master already has an active Pro subscription."
            });
        }

        var trialSubscription =
            new Subscription
            {
                MasterId = master.Id,

                Plan =
                    SubscriptionPlan.Pro,

                Status =
                    SubscriptionStatus.Active,

                IsTrial = true,

                ExpiresAt =
                    now.AddDays(ProTrialDays)
            };

        master.ProTrialUsedAt = now;

        _context.Subscriptions.Add(
            trialSubscription);

        await _context.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Ok(new
        {
            message =
                "Pro trial activated.",

            trialDays =
                ProTrialDays,

            isTrial = true,

            subscription = new
            {
                trialSubscription.Id,
                trialSubscription.MasterId,
                trialSubscription.Plan,
                trialSubscription.Status,
                trialSubscription.IsTrial,
                trialSubscription.ExpiresAt
            }
        });
    }

    // ================================
    // DELETE
    // ================================

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<ActionResult> DeleteById(
        int id)
    {
        var sub =
            await _context.Subscriptions
                .FirstOrDefaultAsync(
                    s => s.Id == id);

        if (sub == null)
        {
            return NotFound(
                "Subscription Not Found.");
        }

        _context.Subscriptions.Remove(sub);

        await _context.SaveChangesAsync();

        return Ok(
            "Subscription is deleted");
    }

    // ================================
    // CREATE MANUALLY
    // ADMIN ONLY
    // ================================

    [HttpPost]
    [Authorize(Roles = "Superadmin")]
    public async Task<ActionResult> Create(
        [FromBody] CreateSubscriptionDTO dto)
    {
        var sub =
            new Subscription
            {
                MasterId =
                    dto.MasterId,

                Plan =
                    dto.Plan,

                Status =
                    dto.Status,

                ExpiresAt =
                    dto.ExpiresAt,

                IsTrial = false
            };

        _context.Subscriptions.Add(sub);

        await _context.SaveChangesAsync();

        return Ok(sub);
    }

    // ================================
    // UPDATE MANUALLY
    // ADMIN ONLY
    // ================================

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<ActionResult> Update(
        int id,
        [FromBody] CreateSubscriptionDTO dto)
    {
        var subToChange =
            await _context.Subscriptions
                .FirstOrDefaultAsync(
                    s => s.Id == id);

        if (subToChange == null)
        {
            return NotFound(
                "Subscription Not Found.");
        }

        subToChange.MasterId =
            dto.MasterId;

        subToChange.Plan =
            dto.Plan;

        subToChange.Status =
            dto.Status;

        subToChange.ExpiresAt =
            dto.ExpiresAt;

        await _context.SaveChangesAsync();

        return Ok(subToChange);
    }

    // ================================
    // HELPERS
    // ================================

    private string? GetCurrentEmail()
    {
        return
            User.FindFirstValue(
                JwtRegisteredClaimNames.Sub)
            ??
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);
    }
}