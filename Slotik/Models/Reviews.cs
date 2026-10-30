namespace Slotik.Models;


public class Review
{
    public int Id { get; set; }
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public int? UserId { get; set; } // TODO after getting rid of seed reviews remove nullable (?)
    public User? User { get; set; } // TODO after getting rid of seed reviews remove nullable (?)
    public decimal? Price {get;set; } // TODO after getting rid of seed reviews remove nullable (?)
    public int? DurationMin { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public string? Reply { get; set; } 
    public DateTimeOffset? UpdatedAt { get; set; }  // TODO after getting rid of seed reviews remove nullable (?)
    public DateTimeOffset? CreatedAt { get;set; }  // TODO after getting rid of seed reviews remove nullable (?)
    public List<ReviewPhoto> Files { get; set; } = [];
}