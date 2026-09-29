namespace Slotik.Models;

public class Master
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int DistrictId { get; set; }
    public District District { get; set; } = null!;

    public string Slug { get; set; } = string.Empty;
    public string? About { get; set; }
    public int ExperienceYears { get; set; }
    public int SlotStepMin { get; set; }
    public bool IsBlocked { get; set; } = false;

    public ICollection<Service> Services { get; set; } = new List<Service>();
    public ICollection<Favorite> FavoritedBy { get; set; } = new List<Favorite>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    public ICollection<DaysOff> DaysOff { get; set; } = new List<DaysOff>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}