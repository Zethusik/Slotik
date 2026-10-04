namespace Slotik.Models
{
    public class PortfolioPhoto
    {
        public int Id { get; set; }

        public string PhotoUrl { get; set; } = string.Empty;
        public string PhotoId { get; set; } = string.Empty;

        public int MasterId { get; set; }
        public Master Master { get; set; } = null!;
    }
}
