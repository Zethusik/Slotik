namespace Slotik.Models
{
    public class ReviewPhoto
    {

        public int Id { get; set; }

        public string PhotoUrl { get; set; } = string.Empty;
        public string PhotoId { get; set; } = string.Empty;

        public int ReviewId { get; set; }
        public Review Review { get; set; } = null!;

       
    }
}
