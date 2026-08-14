namespace Slotik.Models;

public class ServicePhoto
{
    public int Id { get; set; }

    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;

    public string PhotoUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
}