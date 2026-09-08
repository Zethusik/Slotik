namespace Slotik.Models
{
    public class PendingReset
    {
        public int Id { get; set; }
        public string email { get; set; }
        public string codeHash { get; set; }
        public DateTime codeExpiresAt { get; set; }
        public string? finalTokenHash { get; set; }
        public DateTime? finalExpiresAt { get; set; }
    }
}
