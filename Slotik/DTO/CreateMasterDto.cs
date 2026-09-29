namespace Slotik.DTO;

public class CreateMasterDto
{
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public int DistrictId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string? About { get; set; }
    public int ExperienceYears { get; set; }
    public int SlotStepMin { get; set; }
    public bool IsBlocked { get; set; } = false;
}