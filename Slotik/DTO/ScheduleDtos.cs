namespace Slotik.DTO;

public class UpdateScheduleDayDto
{
    public bool IsWorking { get; set; }

    public List<ScheduleIntervalDto> Intervals { get; set; }
        = new();
}

public class ScheduleIntervalDto
{
    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }
}

public class UpdateSlotStepDto
{
    public int SlotStepMin { get; set; }
}