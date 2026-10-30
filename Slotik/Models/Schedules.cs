namespace Slotik.Models;

public class Schedule
{
    public int Id { get; set; }

    // 1 = Monday
    // 2 = Tuesday
    // . . .
    // 7 = Sunday
    public int Weekday { get; set; }

    public bool IsWorking { get; set; } = true;

    public int MasterId { get; set; }
    public Master Master { get; set; } = null!;

    public ICollection<ScheduleInterval> Intervals { get; set; }
        = new List<ScheduleInterval>();
}