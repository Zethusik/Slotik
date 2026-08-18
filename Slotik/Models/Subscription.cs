using Slotik.Models.Enums;

namespace Slotik.Models;

public class Subscription
{
    public int Id { get; set; }

    public int MasterId { get; set; }
    public Master Master { get; set; } = null!;

    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;
    public DateTimeOffset ExpiresAt { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}