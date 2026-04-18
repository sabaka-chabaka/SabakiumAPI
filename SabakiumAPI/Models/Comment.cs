namespace SabakiumAPI.Models;

public class Comment
{
    public int Id { get; set; }
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}