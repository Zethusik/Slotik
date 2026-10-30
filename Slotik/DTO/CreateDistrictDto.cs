namespace Slotik.DTO;

public class CreateDistrictDto
{
    [System.ComponentModel.DataAnnotations.Required] public string Name { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] public int CityId { get; set; }
}
