namespace Slotik.Models;

public class Service
{
    public int Id { get; set; }

    public int MasterId { get; set; }
    public Master Master { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public int DurationMin { get; set; }
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public string? Included { get; set; }

    public int? GroupId { get; set; }
    public ServiceGroup? Group { get; set; }

    public bool IsPopular { get; set; } = false;

    public int SortOrder { get; set; } = 0;

    public ICollection<ServicePhoto> Photos { get; set; } = new List<ServicePhoto>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}