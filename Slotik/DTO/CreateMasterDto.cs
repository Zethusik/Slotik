namespace Slotik.DTO;

public class CreateMasterDto
{
    
    public int CategoryId { get; set; }
    public int DistrictId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string? About { get; set; }
    public int ExperienceYears { get; set; }
    public int SlotStepMin { get; set; }
    public bool IsBlocked { get; set; } = false;

    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}