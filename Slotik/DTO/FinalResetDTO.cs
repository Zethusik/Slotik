using System.ComponentModel.DataAnnotations;

namespace Slotik.DTO
{
    public class FinalResetDTO
    {
        [Required(ErrorMessage = "Token is required")]
        [MinLength(6, ErrorMessage = "Token must contain at least 6 characters")]
        public string Token { get; set; }
        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must contain at least 6 characters")]
        public string NewPassword { get; set; }
    }
}
