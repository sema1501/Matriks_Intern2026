namespace CryptoTracker.API.Models;

public class Feedback
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string Message { get; set; } = string.Empty;

    public int? Rating { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}