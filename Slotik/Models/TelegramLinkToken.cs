namespace Slotik.Models
{
    public class TelegramLinkToken
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Token { get; set; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; set; }

        public DateTimeOffset? UsedAt { get; set; }

        public User User { get; set; } = null!;
    }
}
