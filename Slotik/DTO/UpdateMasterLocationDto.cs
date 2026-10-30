namespace Slotik.DTO;

public class UpdateMasterLocationDto
{
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] public int? DistrictId { get; set; }
    public string? Address { get; set; }
    [System.ComponentModel.DataAnnotations.Range(-90d, 90d)] public double? Latitude { get; set; }
    [System.ComponentModel.DataAnnotations.Range(-180d, 180d)] public double? Longitude { get; set; }
}
