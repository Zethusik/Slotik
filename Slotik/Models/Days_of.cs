namespace Slotik.Models;

public class DaysOff
{
    public int Id { get; set; }
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }

    public int MasterId { get; set; }
    public Master Master { get; set; } = null!;
}