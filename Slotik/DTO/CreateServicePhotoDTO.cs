using System.ComponentModel.DataAnnotations;

namespace Slotik.DTO
{
    public class CreateServicePhotoDTO
    {
        [Required(ErrorMessage = "Service Id is required")]
        public int ServiceId { get; set; }
        [Required(ErrorMessage = "Photo Url is required")]
        public string PhotoUrl { get; set; }
    }
}
