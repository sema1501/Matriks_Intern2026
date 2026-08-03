namespace CryptoTracker.API.Models;

public class TradingBot
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Symbol { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public decimal BuyRsiThreshold { get; set; } = 30m;
    public decimal SellRsiThreshold { get; set; } = 70m;
    public decimal TradeQuantity { get; set; }

    public ICollection<BotSignal> Signals { get; set; } = new List<BotSignal>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}