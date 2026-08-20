using Slotik.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Slotik.DTO
{
    public class RegisterDTO
    {
        // very cool feature i discovered here, just not to do checks in controller, very useful!
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = String.Empty;
        [Required(ErrorMessage = "Phone is required")]
        public string Phone { get; set; } = String.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email")]
        public string Email { get; set; } = String.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must contain at least 6 characters")]
        public string Password { get; set; } = String.Empty;
        [Required(ErrorMessage = "Role is required")]
        public string Role { get; set; } = UserRole.Client.ToString();

        
    }
}
