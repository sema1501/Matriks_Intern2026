namespace CryptoTracker.API.Models;

public class WatchlistItem
{
    public int    Id        { get; set; }
    public int    UserId    { get; set; }
    public string Symbol    { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}
