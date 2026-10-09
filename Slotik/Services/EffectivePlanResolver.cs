using Slotik.Models;
using Slotik.Models.Enums;

namespace Slotik.Services;

public static class EffectivePlanResolver
{
    // Paid here means a non-trial paid-tier row, including an explicit admin grant.
    public static Subscription? Resolve(IEnumerable<Subscription> subscriptions, DateTimeOffset now) => subscriptions
        .Where(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > now && Priority(s) > 0)
        .OrderByDescending(Priority).ThenByDescending(s => s.Id).FirstOrDefault();

    private static int Priority(Subscription s) => (s.Plan, s.IsTrial) switch
    {
        (SubscriptionPlan.Pro, false) => 4,
        (SubscriptionPlan.Pro, true) => 3,
        (SubscriptionPlan.Basic, false) => 2,
        (SubscriptionPlan.Free, false) => 1,
        _ => 0
    };
}
