namespace SabakiumAPI.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public int RecipientId { get; set; }
    public User Recipient { get; set; } = null!;

    public string Ciphertext { get; set; } = "";
    public string Iv { get; set; } = "";
    public string AuthTag { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}