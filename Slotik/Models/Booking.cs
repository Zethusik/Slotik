using Slotik.Models.Enums;

namespace Slotik.Models;

public class Booking
{
    public int Id { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public string? Comment { get; set; }
    public bool ReminderSent { get; set; }

    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;

    public int MasterId { get; set; }
    public Master Master { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public Review? Review { get; set; }
}