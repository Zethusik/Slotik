namespace Slotik.Models;

public class Favorite
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int MasterId { get; set; }
    public Master Master { get; set; } = null!;
}