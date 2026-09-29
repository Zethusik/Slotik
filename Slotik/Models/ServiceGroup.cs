namespace Slotik.Models
{
    public class ServiceGroup
    {

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        public ICollection<Service> Services { get; set; }
            = new List<Service>();

    }
}
