using System.ComponentModel.DataAnnotations;
namespace Slotik.DTO
{
    public class PasswordRestoreDTOcs
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email")]
        public string Email { get; set; }
    }
}
