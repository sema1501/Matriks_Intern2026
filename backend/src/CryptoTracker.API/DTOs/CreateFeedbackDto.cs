namespace CryptoTracker.API.DTOs;

public class CreateFeedbackDto
{
    public string Message { get; set; } = string.Empty;

    public int? Rating { get; set; }
}