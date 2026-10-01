using Slotik.Models.Enums;

namespace Slotik.DTO;

public class CreatePaymentDto
{
    public SubscriptionPlan Plan { get; set; }
}
