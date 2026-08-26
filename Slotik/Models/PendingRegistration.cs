using Slotik.Models.Enums;

namespace Slotik.Models
{
    public class PendingRegistration
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string PasswordHash {  get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string TokenHash {  get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
