using Slotik.Models.Enums;

namespace Slotik.DTO;

public class CreateBookingDto
{
    public DateTimeOffset StartsAt { get; set; }

    public int ServiceId { get; set; }

    public string? Comment { get; set; }
}