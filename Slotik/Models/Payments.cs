using Slotik.Models.Enums;

namespace Slotik.Models;

public class Payment
{
    public int Id { get; set; }
    public DateTimeOffset PaidAt { get; set; } = DateTimeOffset.UtcNow;
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Success;

    public int SubscriptionId { get; set; }
    public Subscription Subscription { get; set; } = null!;
}