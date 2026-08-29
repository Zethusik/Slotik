using System.ComponentModel.DataAnnotations;

namespace Slotik.DTO
{
    public class CreateServiceDTO
    {
        [Required(ErrorMessage = "Master id is required")]
        public int MasterId { get; set; }
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Price is required")]
        public decimal Price { get; set; }
        [Required(ErrorMessage = "Duration is required")]
        public int DurationMin { get; set; }
    }
}
