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
    Expired,
    Failed
}

/// <summary>Sinyali neyin ürettiği. Sayısal değerler veritabanında saklanır, değiştirme.</summary>
public enum BotSignalReason
{
    /// <summary>Botun stratejisi (RSI/EMA). Eski kayıtlar da bu değere düşer.</summary>
    Strategy = 0,
    StopLoss = 1,
    TakeProfit = 2
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
    public BotSignalReason Reason { get; set; } = BotSignalReason.Strategy;
}
