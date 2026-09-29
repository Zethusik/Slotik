using System.ComponentModel.DataAnnotations;

namespace Slotik.DTO
{
    public class CreateServiceGroupDTO
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public int CategoryId { get; set; }
    }
}
