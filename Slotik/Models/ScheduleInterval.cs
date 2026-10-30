namespace Slotik.Models;

public class ScheduleInterval
{
    public int Id { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int ScheduleId { get; set; }

    public Schedule Schedule { get; set; } = null!;
}