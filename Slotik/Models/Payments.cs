using Slotik.Models.Enums;

namespace Slotik.Models;

public class Payment
{
    public int Id { get; set; }

    public string OrderId { get; set; } = string.Empty;
    public string? ProviderPaymentId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAt { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "UAH";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? ProviderStatus { get; set; }

    public int SubscriptionId { get; set; }
    public Subscription Subscription { get; set; } = null!;
}
