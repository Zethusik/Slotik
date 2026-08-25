namespace Slotik.DTO;

public class CreateDaysOffDto
{
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public int MasterId { get; set; }
}