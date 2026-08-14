namespace Slotik.Models;

public class Schedule
{
    public int Id { get; set; }
    public int Weekday { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public TimeOnly? BreakStart { get; set; }
    public TimeOnly? BreakEnd { get; set; }
    public bool IsWorking { get; set; } = true;

    public int MasterId { get; set; }
    public Master Master { get; set; } = null!;
}