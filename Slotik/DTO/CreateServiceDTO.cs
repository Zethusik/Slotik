using System.ComponentModel.DataAnnotations;

namespace Slotik.DTO
{
    public class CreateServiceDTO
    {
        [Required(ErrorMessage = "Master id is required")]
        public int MasterId { get; set; }
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = string.Empty;
        [Required(ErrorMessage = "Price is required")]
        [Range(typeof(decimal), "0", "79228162514264337593543950335")]
        public decimal Price { get; set; }
        [Required(ErrorMessage = "Duration is required")]
        [Range(1, int.MaxValue)]
        public int DurationMin { get; set; }

        [Range(1, int.MaxValue)] public int? GroupId { get; set; }

        public bool IsPopular { get; set; }

        public int SortOrder { get; set; }

        public List<IFormFile> files { get; set; } = new List<IFormFile>();
    }
}
