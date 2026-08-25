using Slotik.Models.Enums;

namespace Slotik.DTO;

public class CreateBookingDto
{
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public string? Comment { get; set; }
    public bool ReminderSent { get; set; } = false;

    public int ServiceId { get; set; }
    public int MasterId { get; set; }
    public int UserId { get; set; }
}