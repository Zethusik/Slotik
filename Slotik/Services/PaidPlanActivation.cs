using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.Models;
using Slotik.Models.Enums;

namespace Slotik.Services;

public static class PaidPlanActivation
{
    // Caller owns the master lock and transaction, and has verified the provider result.
    public static async Task<bool> ApplyAsync(AppDbContext db, Payment payment, DateTimeOffset now, CancellationToken ct)
    {
        var target = payment.Subscription;
        var other = await db.Subscriptions.Where(s => s.MasterId == target.MasterId && s.Id != target.Id)
            .ToListAsync(ct);
        if (target.Plan is not (SubscriptionPlan.Basic or SubscriptionPlan.Pro)
            || other.Any(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > now
                && !s.IsTrial && s.Plan == target.Plan))
        {
            // Keep the verified financial success. An operator must resolve money received
            // for an old/racing checkout; never silently grant another month or refund it.
            payment.EntitlementReviewRequired = true;
            target.Status = SubscriptionStatus.Cancelled;
            target.ExpiresAt = now;
            return false;
        }

        // New policy forfeits all old paid/trial remaining access on a successful switch.
        // Keep original boundaries and financial history for audit. Truncate current grant
        // boundaries so a subsequent refund cannot resurrect forfeited time via H12.
        var oldGrants = await db.EntitlementGrants.Where(g => g.MasterId == target.MasterId
            && g.SourceSubscriptionId != target.Id && g.RevokedAt == null && g.EndsAt > now).ToListAsync(ct);
        foreach (var grant in oldGrants)
        {
            if (grant.StartsAt > now) grant.StartsAt = now;
            grant.EndsAt = now;
        }
        foreach (var old in other.Where(s => s.Status == SubscriptionStatus.Active
            && s.Plan is SubscriptionPlan.Basic or SubscriptionPlan.Pro))
            old.Status = SubscriptionStatus.Cancelled;

        target.IsTrial = false;
        target.Status = SubscriptionStatus.Active;
        target.ExpiresAt = now.AddDays(30);
        await EntitlementService.RecordPaymentAsync(db, payment, null, null, now, ct);
        return true;
    }
}
