namespace Slotik.DTO
{
    public class ReviewDTO
    {
        public int id { get; set; }
        public int rating { get; set; }
        public string? text { get; set; } = string.Empty;
        public DateTimeOffset? createdAt { get; set; }
        public string? clientFirstName { get; set; } = string.Empty;
        public string? clientAvatarUrl { get; set; } = string.Empty;
        public string serviceName { get; set; } = string.Empty ;
        public int durationMin { get; set; }
        public decimal price {  get; set; }
        public DateTimeOffset? startsAt { get; set; }
        public DateTimeOffset? endsAt { get;set; }
        public string? reply { get; set; }


    }
}
