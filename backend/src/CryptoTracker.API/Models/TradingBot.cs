namespace CryptoTracker.API.Models;

/// <summary>
/// Botun hangi sinyal mantığıyla çalıştığını belirler.
/// </summary>
public enum BotStrategy
{
    /// <summary>
    /// Mevcut davranış: RSI eşiği.
    /// Sayısal değeri 0 OLMAK ZORUNDA — veritabanındaki eski kayıtlar bu değere düşer.
    /// </summary>
    RsiThreshold = 0,

    /// <summary>Kısa ve uzun EMA'nın kesişimi.</summary>
    EmaCrossover = 1
}

public class TradingBot
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Symbol { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsFlagged { get; set; }

    // ---------- YENİ: strateji seçimi ----------
    public BotStrategy Strategy { get; set; } = BotStrategy.RsiThreshold;

    // ---------- MEVCUT: RSI parametreleri (dokunulmadı) ----------
    public decimal BuyRsiThreshold { get; set; } = 30m;
    public decimal SellRsiThreshold { get; set; } = 70m;

    // ---------- YENİ: EMA parametreleri ----------
    // int? (nullable) çünkü RSI botlarında bu alanlar BOŞ kalmalı.
    // 0 yazmak "periyot sıfır" demektir ve yanlıştır.
    public int? ShortEmaPeriod { get; set; }
    public int? LongEmaPeriod { get; set; }

    public decimal TradeQuantity { get; set; }

    public ICollection<BotSignal> Signals { get; set; } = new List<BotSignal>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Bot kaydedilmeden önce çağrılır. Hatalı bot veritabanına HİÇ yazılmasın.
    /// </summary>
    public void Validate()
    {
        if (TradeQuantity <= 0m)
            throw new ArgumentException("İşlem miktarı sıfırdan büyük olmalı.");

        switch (Strategy)
        {
            case BotStrategy.RsiThreshold:
                if (BuyRsiThreshold >= SellRsiThreshold)
                    throw new ArgumentException("Alım eşiği satım eşiğinden küçük olmalı.");
                break;

            case BotStrategy.EmaCrossover:
                if (ShortEmaPeriod is null || LongEmaPeriod is null)
                    throw new ArgumentException("EMA stratejisi için kısa ve uzun periyot zorunlu.");
                if (ShortEmaPeriod < 1 || LongEmaPeriod < 1)
                    throw new ArgumentException("EMA periyotları en az 1 olmalı.");
                if (ShortEmaPeriod >= LongEmaPeriod)
                    throw new ArgumentException("Kısa EMA periyodu uzun EMA periyodundan küçük olmalı.");
                break;

            default:
                throw new NotSupportedException($"Bilinmeyen strateji: {Strategy}");
        }
    }
}