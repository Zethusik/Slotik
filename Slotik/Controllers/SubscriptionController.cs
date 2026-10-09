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
    private readonly IMasterSubscriptionLock _masterLock;

    public SubscriptionController(
        AppDbContext context, LiqPayService liqPay, IMasterSubscriptionLock masterLock)
    {
        _context = context;
        _liqPay = liqPay;
        _masterLock = masterLock;
    }


    [HttpPost("free")]
    [Authorize(Roles = "Master")]

    public async Task<ActionResult> AssignFree(CancellationToken cancellationToken)
    {
        if (User.UserId() is not int userId) return Unauthorized();
        var masterId = await _context.Masters.Where(m => m.UserId == userId)
            .Select(m => (int?)m.Id).SingleOrDefaultAsync(cancellationToken);
        if (masterId == null) return NotFound(new { message = "Master profile not found." });

        await using var transaction = await _masterLock.AcquireAsync(masterId.Value, cancellationToken);
        // Re-read under the lock: another request may have just committed the Free row.
        var subscriptions = await _context.Subscriptions.Where(s => s.MasterId == masterId.Value)
            .Include(s => s.Payments).ToListAsync(cancellationToken);
        var free = subscriptions.FirstOrDefault(s => s.Plan == SubscriptionPlan.Free
            && s.Status == SubscriptionStatus.Active && s.ExpiresAt == DateTimeOffset.MaxValue && !s.IsTrial);
        if (free != null)
        {
            await OnboardingService.SaveAndCompleteAsync(_context, masterId.Value, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(await ResponseAsync(free, cancellationToken));
        }
        // Preserve the existing refusal to recreate/reactivate historical Free subscriptions.
        if (subscriptions.Any(s => s.Plan == SubscriptionPlan.Free))
            return BadRequest(new { error = "existing_free_subscription", message = "An existing Free subscription requires review." });
        if (subscriptions.Any(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > DateTimeOffset.UtcNow
            && s.Plan is SubscriptionPlan.Basic or SubscriptionPlan.Pro))
            return Conflict(new { error = "active_paid_subscription", message = "An active paid subscription cannot be replaced by Free." });

        free = new Subscription { MasterId = masterId.Value, Plan = SubscriptionPlan.Free,
            Status = SubscriptionStatus.Active, ExpiresAt = DateTimeOffset.MaxValue, IsTrial = false };
        _context.Subscriptions.Add(free);
        await OnboardingService.SaveAndCompleteAsync(_context, masterId.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(await ResponseAsync(free, cancellationToken));
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

            return Ok(Responses(allSubscriptions));
        }

        var userId = User.UserId();

        if (!userId.HasValue)
            return Unauthorized();

        var subscriptions =
            await _context.Subscriptions
                .AsNoTracking()
                .Include(s => s.Payments)
                .Include(s => s.Master)
                    .ThenInclude(m => m.User)
                .Where(s =>
                    s.Master.UserId == userId.Value)
                .ToListAsync();

        return Ok(Responses(subscriptions));
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
            return Ok(await ResponseAsync(sub));

        var userId = User.UserId();

        if (!userId.HasValue)
            return Unauthorized();

        if (sub.Master.UserId != userId.Value)
        {
            return Forbid();
        }

        return Ok(await ResponseAsync(sub));
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
        var userId = User.UserId();

        if (!userId.HasValue)
            return Unauthorized();

        var master =
            await _context.Masters
                .AsNoTracking()
                .Include(m => m.User)
                .FirstOrDefaultAsync(
                    m => m.UserId == userId.Value,
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
        var userId = User.UserId();

        if (!userId.HasValue)
            return Unauthorized();

        var masterId =
            await _context.Masters
                .Where(m =>
                    m.UserId == userId.Value)
                .Select(m =>
                    (int?)m.Id)
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (masterId == null)
        {
            return NotFound(
                "Master not found.");
        }

        await using var transaction = await _masterLock.AcquireAsync(masterId.Value, cancellationToken);

        var master =
            await _context.Masters
                .FirstOrDefaultAsync(
                    m => m.Id == masterId.Value,
                    cancellationToken);

        if (master == null)
        {
            return NotFound(
                "Master not found.");
        }

        // Trial can only used
        // once per acc
        if (master.ProTrialUsedAt != null)
        {
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

        await OnboardingService.SaveAndCompleteAsync(_context, master.Id, cancellationToken);

        await EntitlementService.RecordTrialAsync(_context, trialSubscription, now, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Ok(new
        {
            message =
                "Pro trial activated.",

            trialDays =
                ProTrialDays,

            isTrial = true,

            subscription = await ResponseAsync(trialSubscription, cancellationToken)
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
        var ownerId = await _context.Subscriptions.AsNoTracking().Where(s => s.Id == id)
            .Select(s => (int?)s.MasterId).SingleOrDefaultAsync();
        if (ownerId == null) return NotFound();
        await using var billingTransaction = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ownerId.Value})");

        if (await _context.EntitlementGrants.AnyAsync(g => g.SourceSubscriptionId == id))
            return Conflict(new { error = "managed_entitlement", message = "Use the payment/refund flow for managed billing periods." });
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
        await billingTransaction.CommitAsync();

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
        if (!await _context.Masters.AnyAsync(m => m.Id == dto.MasterId)) return NotFound("Master not found");
        await using var billingTransaction = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({dto.MasterId})");
        if (dto.Plan != SubscriptionPlan.Free && await _context.EntitlementGrants.AnyAsync(g => g.MasterId == dto.MasterId))
            return Conflict(new { error = "managed_entitlement" });

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

        await OnboardingService.SaveAndCompleteAsync(_context, dto.MasterId);
        await billingTransaction.CommitAsync();

        return Ok(await ResponseAsync(sub));
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
        if (!await _context.Masters.AnyAsync(m => m.Id == dto.MasterId)) return NotFound("Master not found");
        var ownerId = await _context.Subscriptions.AsNoTracking().Where(s => s.Id == id)
            .Select(s => (int?)s.MasterId).SingleOrDefaultAsync();
        if (ownerId == null) return NotFound();
        await using var billingTransaction = await _context.Database.BeginTransactionAsync();
        foreach (var masterId in new[] { ownerId.Value, dto.MasterId }.Distinct().Order())
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({masterId})");
        if (await _context.EntitlementGrants.AnyAsync(g => g.MasterId == ownerId.Value || g.MasterId == dto.MasterId))
            return Conflict(new { error = "managed_entitlement" });

        if (await _context.EntitlementGrants.AnyAsync(g => g.SourceSubscriptionId == id))
            return Conflict(new { error = "managed_entitlement", message = "Use the payment/refund flow for managed billing periods." });
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
        await billingTransaction.CommitAsync();

        return Ok(await ResponseAsync(subToChange));
    }

    // ================================
    // HELPERS
    // ================================

    private IEnumerable<SubscriptionResponse> Responses(List<Subscription> subscriptions)
    {
        var now = DateTimeOffset.UtcNow;
        var effective = subscriptions.GroupBy(s => s.MasterId)
            .ToDictionary(g => g.Key, g => EffectivePlanResolver.Resolve(g, now)?.Id);
        return subscriptions.Select(s => ApiResponses.Subscription(s, _liqPay, effective[s.MasterId]));
    }

    private async Task<SubscriptionResponse> ResponseAsync(Subscription subscription, CancellationToken ct = default)
    {
        var all = await _context.Subscriptions.AsNoTracking().Where(s => s.MasterId == subscription.MasterId).ToListAsync(ct);
        return ApiResponses.Subscription(subscription, _liqPay, EffectivePlanResolver.Resolve(all, DateTimeOffset.UtcNow)?.Id);
    }

}
