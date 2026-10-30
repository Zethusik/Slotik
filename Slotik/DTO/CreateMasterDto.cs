using Slotik.Models;

namespace Slotik.DTO;

using System.ComponentModel.DataAnnotations;

public class CreateMasterDto
{
    
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
    [Range(1, int.MaxValue)] public int DistrictId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string? About { get; set; }
    [Range(0, int.MaxValue)] public int ExperienceYears { get; set; }
    [SlotStep] public int SlotStepMin { get; set; }


    public string? Address { get; set; }
    [Range(-90d, 90d)] public double? Latitude { get; set; }
    [Range(-180d, 180d)] public double? Longitude { get; set; }

    public ICollection<IFormFile>? portfolioPhotos { get; set; } = new List<IFormFile>();
}
