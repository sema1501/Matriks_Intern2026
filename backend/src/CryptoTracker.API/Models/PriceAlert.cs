namespace CryptoTracker.API.Models;

public enum AlertDirection
{
    Above = 0,
    Below = 1
}

public class PriceAlert
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal TargetPrice { get; set; }
    public AlertDirection Direction { get; set; }
    public bool IsTriggered { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
