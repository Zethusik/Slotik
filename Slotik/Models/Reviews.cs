namespace Slotik.Models;

public class Review
{
    public int Id { get; set; }
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;

    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
}