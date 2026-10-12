namespace Slotik.Models
{
    public class TelegramNotification
    {
        public long Id { get; set; }

        public int UserId { get; set; }

        public string Message { get; set; } = string.Empty;

        public DateTimeOffset SendAt { get; set; }
            = DateTimeOffset.UtcNow;

        public int Attempts { get; set; } = 0;
    }
}
