namespace Slotik.DTO;

public class UpdateMasterLocationDto
{
    public int? DistrictId { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}