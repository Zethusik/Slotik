namespace Slotik.Models
{
    public class TelegramBinding
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public long ChatId { get; set; }

        public long TelegramUserId { get; set; }

        public string? Username { get; set; }

        public DateTimeOffset LinkedAt { get; set; }

        public User User { get; set; } = null!;
    }
}
