using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.Models;
using Slotik.Models.Enums;

namespace Slotik.Services;

public static class OnboardingService
{
    // Save the successful step and the monotonic completion flag in the same transaction.
    // Existing callback/trial/schedule transactions remain owned by their callers.
    public static async Task SaveAndCompleteAsync(AppDbContext db, int masterId, CancellationToken ct = default)
    {
        var ownsTransaction = db.Database.IsRelational() && db.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        await db.SaveChangesAsync(ct);
        await TryCompleteAsync(db, masterId, ct);
        if (transaction != null) await transaction.CommitAsync(ct);
    }

    public static async Task TryCompleteAsync(AppDbContext db, int masterId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var eligible = await EligibleMasters(db, now).AnyAsync(m => m.Id == masterId, ct);
        if (!eligible) return;
        var master = await db.Masters.SingleAsync(m => m.Id == masterId, ct);
        master.IsOnboardingCompleted = true;
        await db.SaveChangesAsync(ct);
    }

    public static IQueryable<Master> EligibleMasters(AppDbContext db, DateTimeOffset now) =>
        db.Masters.Where(m => !m.IsOnboardingCompleted
            && m.Category != null && m.About != null && m.About.Trim() != ""
            && m.Services.Any()
            && m.Schedules.Any(s => s.IsWorking && s.Intervals.Any())
            && m.Subscriptions.Any(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > now
                && (s.Plan == SubscriptionPlan.Free
                    || (s.IsTrial && s.Plan == SubscriptionPlan.Pro && m.ProTrialUsedAt != null)
                    || s.Payments.Any(p => p.Status == PaymentStatus.Success && p.PaidAt != null))));
}
