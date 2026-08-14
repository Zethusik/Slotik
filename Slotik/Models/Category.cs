namespace Slotik.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    public ICollection<Master> Masters { get; set; } = new List<Master>();
}