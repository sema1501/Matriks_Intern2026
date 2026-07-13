namespace CryptoTracker.API.DTOs;

public class FeedbackDto
{
    public int Id { get; set; }

    public string Message { get; set; } = string.Empty;

    public int? Rating { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? UserId { get; set; }
}