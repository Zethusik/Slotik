using Slotik.Models.Enums;
using System.ComponentModel.DataAnnotations;
namespace Slotik.DTO

{
    public class CreateSubscriptionDTO
    {
        [Required(ErrorMessage = "Id is required")]
        public int MasterId { get; set; }

        [Required(ErrorMessage = "Plan is required")]
        public SubscriptionPlan Plan { get; set; }
        [Required(ErrorMessage = "Expiry date is required")]
        public DateTimeOffset ExpiresAt { get; set; }
        [Required(ErrorMessage = "Satus required")]
        public SubscriptionStatus Status { get; set; }

    }
}
