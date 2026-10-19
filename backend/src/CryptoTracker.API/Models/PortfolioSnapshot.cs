namespace CryptoTracker.API.Models;

/// <summary>
/// Kullanıcının portföy değerinin belli bir andaki kaydı. PortfolioSnapshotService saatte bir yazar;
/// 30 günden eski kayıtlar günde bire indirgenir.
/// </summary>
public class PortfolioSnapshot
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Kaydın alındığı an (UTC).</summary>
    public DateTime TakenAt { get; set; }

    /// <summary>O andaki sanal nakit bakiye (User.VirtualBalance).</summary>
    public decimal CashBalance { get; set; }

    /// <summary>Coinlerin o anki Binance fiyatıyla toplam değeri.</summary>
    public decimal HoldingsValue { get; set; }

    public decimal TotalValue { get; set; }
}
