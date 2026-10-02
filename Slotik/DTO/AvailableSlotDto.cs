using Microsoft.AspNetCore.Mvc;

namespace Slotik.DTO;

public class AvailableSlotDto
{
    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }
}