using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.Models;
using Slotik.Models.Enums;

namespace Slotik.Services;

public static class EntitlementService
{
    public static async Task RecordTrialAsync(AppDbContext db, Subscription trial, DateTimeOffset now, CancellationToken ct)
    {
        if (await db.EntitlementGrants.AnyAsync(g => g.SourceSubscriptionId == trial.Id, ct)) return;
        db.EntitlementGrants.Add(Create(trial, EntitlementSource.Trial, now, now, trial.ExpiresAt, Guid.NewGuid()));
    }

    // Called after the existing activation policy calculates the aggregate expiration,
    // under the same master advisory lock and callback transaction.
    public static async Task RecordPaymentAsync(AppDbContext db, Payment payment,
        Subscription? previousTrial, Subscription? previousPaid, DateTimeOffset now, CancellationToken ct)
    {
        if (await db.EntitlementGrants.AnyAsync(g => g.PaymentId == payment.Id, ct)) return;
        var sources = new Dictionary<int, EntitlementGrant>();
        foreach (var sub in new[] { previousTrial, previousPaid }.OfType<Subscription>())
        {
            var grant = await db.EntitlementGrants.SingleOrDefaultAsync(g => g.SourceSubscriptionId == sub.Id, ct);
            if (grant == null)
            {
                // Preserve known current access as an opaque legacy source. Do not guess
                // which historical payment purchased this aggregate interval.
                grant = Create(sub, sub.IsTrial ? EntitlementSource.Trial : EntitlementSource.Legacy,
                    now.AddTicks(-1), now, sub.ExpiresAt, Guid.NewGuid());
                db.EntitlementGrants.Add(grant);
            }
            sources[sub.Id] = grant;
        }
        var start = payment.Subscription.ExpiresAt.AddDays(-30);
        var carried = new[] { previousTrial, previousPaid }.OfType<Subscription>()
            .Where(s => s.Plan == payment.Subscription.Plan && s.ExpiresAt == start)
            .OrderByDescending(s => s.Id).FirstOrDefault();
        var chain = carried == null ? Guid.NewGuid() : sources[carried.Id].ChainId;
        var own = Create(payment.Subscription, EntitlementSource.Payment, now, start,
            payment.Subscription.ExpiresAt, chain);
        own.PaymentId = payment.Id;
        db.EntitlementGrants.Add(own);
    }

    public static async Task RevokeAsync(AppDbContext db, Payment payment, DateTimeOffset now, CancellationToken ct)
    {
        var grants = await db.EntitlementGrants.Where(g => g.MasterId == payment.Subscription.MasterId)
            .Include(g => g.SourceSubscription).ToListAsync(ct);
        var own = grants.SingleOrDefault(g => g.PaymentId == payment.Id);
        if (own == null)
        {
            if (payment.Subscription.Status == SubscriptionStatus.Pending && payment.PaidAt == null)
            {
                payment.Subscription.Status = SubscriptionStatus.Cancelled;
                return;
            }
            // Historical aggregate expiry does not prove per-payment ownership of time.
            // Record the reversal for investigation without confiscating other paid access.
            payment.EntitlementReviewRequired = true;
            return;
        }
        if (own.RevokedAt != null) return;
        var activePaid = await db.Subscriptions.Where(s => s.MasterId == own.MasterId
            && s.Status == SubscriptionStatus.Active && !s.IsTrial && s.Plan != SubscriptionPlan.Free && s.ExpiresAt > now)
            .OrderByDescending(s => s.ExpiresAt).FirstOrDefaultAsync(ct);
        var activeChain = activePaid == null ? null : grants.FirstOrDefault(g => g.SourceSubscriptionId == activePaid.Id);
        var changesCurrentPaid = activePaid == null || activeChain?.ChainId == own.ChainId;
        own.RevokedAt = now;
        own.SourceSubscription.Status = SubscriptionStatus.Cancelled;

        // Compact only the unconsumed remaining time in this renewal chain. Other plans'
        // independent periods retain their original boundaries and calendar consumption.
        var cursor = now;
        foreach (var grant in grants.Where(g => g.ChainId == own.ChainId && g.RevokedAt == null && g.EndsAt > now)
                     .OrderBy(g => g.StartsAt).ThenBy(g => g.Id))
        {
            var remaining = grant.EndsAt - (grant.StartsAt > now ? grant.StartsAt : now);
            grant.StartsAt = cursor; grant.EndsAt = cursor.Add(remaining); cursor = grant.EndsAt;
        }
        var surviving = grants.Where(g => g.RevokedAt == null && g.EndsAt > now).ToList();
        var groups = surviving.GroupBy(g => g.ChainId).ToList();
        if (changesCurrentPaid)
        {
            // The newest surviving paid chain has precedence, preserving existing plan-switch behavior.
            var winner = groups.Where(g => g.Any(s => s.Source != EntitlementSource.Trial))
                .OrderByDescending(g => g.Where(s => s.Source != EntitlementSource.Trial).Max(s => s.ActivatedAt))
                .ThenByDescending(g => g.Max(s => s.SourceSubscriptionId)).FirstOrDefault();
            foreach (var grant in grants.Where(g => !g.SourceSubscription.IsTrial
                         && g.SourceSubscription.Status == SubscriptionStatus.Active))
                grant.SourceSubscription.Status = SubscriptionStatus.Cancelled;
            if (winner != null)
            {
                var source = winner.Where(g => g.Source != EntitlementSource.Trial)
                    .OrderByDescending(g => g.ActivatedAt).ThenByDescending(g => g.SourceSubscriptionId).First();
                source.SourceSubscription.Status = SubscriptionStatus.Active;
                source.SourceSubscription.ExpiresAt = winner.Max(g => g.EndsAt);
            }
        }
        // Restore a still-unconsumed trial if the refunded Pro payment had consumed its active row.
        foreach (var group in groups.Where(g => g.All(s => s.Source == EntitlementSource.Trial)))
        {
            var trial = group.OrderByDescending(g => g.ActivatedAt).First();
            trial.SourceSubscription.Status = SubscriptionStatus.Active;
            trial.SourceSubscription.ExpiresAt = group.Max(g => g.EndsAt);
        }
    }

    private static EntitlementGrant Create(Subscription sub, EntitlementSource source, DateTimeOffset activatedAt,
        DateTimeOffset start, DateTimeOffset end, Guid chain) => new()
    {
        MasterId = sub.MasterId, SourceSubscriptionId = sub.Id, Source = source, Plan = sub.Plan,
        ChainId = chain, ActivatedAt = activatedAt, StartsAt = start, EndsAt = end,
        OriginalStartsAt = start, OriginalEndsAt = end
    };
}
