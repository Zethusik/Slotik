using Slotik.Models.Enums;
namespace Slotik.Models;

public enum EntitlementSource { Payment, Trial, Legacy }

// A source owns its remaining time; renewal does not copy that time into another payment.
public class EntitlementGrant
{
    public int Id { get; set; }
    public Guid ChainId { get; set; }
    public int MasterId { get; set; }
    public int SourceSubscriptionId { get; set; }
    public Subscription SourceSubscription { get; set; } = null!;
    public int? PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public EntitlementSource Source { get; set; }
    public SubscriptionPlan Plan { get; set; }
    public DateTimeOffset ActivatedAt { get; set; }
    public DateTimeOffset OriginalStartsAt { get; set; }
    public DateTimeOffset OriginalEndsAt { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
