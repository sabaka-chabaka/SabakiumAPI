namespace SabakiumAPI.Models;

public class Post
{
    public int Id { get; set; }
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
