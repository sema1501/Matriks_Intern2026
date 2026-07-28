namespace CryptoTracker.API.Models;

public enum BotSignalType
{
    Buy,
    Sell
}

public enum BotSignalStatus
{
    Pending,
    Approved,
    Rejected,
    Expired
}

public class BotSignal
{
    public int Id { get; set; }
    public int BotId { get; set; }
    public TradingBot Bot { get; set; } = null!;
    public BotSignalType SignalType { get; set; }
    public decimal RsiValueAtSignal { get; set; }
    public decimal PriceAtSignal { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public BotSignalStatus Status { get; set; } = BotSignalStatus.Pending;
}
