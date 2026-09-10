namespace CryptoTracker.API.Models;

public enum AlertDirection
{
    Above = 0,
    Below = 1
}

/// <summary>
/// Alarm tipi. <see cref="Price"/> sabit hedef fiyata göre çalışır (mevcut davranış);
/// <see cref="PercentChange"/> ise referans fiyattan yüzde sapmaya göre tetiklenir (Görev 46).
/// </summary>
public enum AlertType
{
    Price = 0,
    PercentChange = 1
}

public class PriceAlert
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal TargetPrice { get; set; }
    public AlertDirection Direction { get; set; }

    /// <summary>Alarm tipi. Varsayılan <see cref="AlertType.Price"/> (geriye uyumluluk).</summary>
    public AlertType Type { get; set; } = AlertType.Price;

    /// <summary>
    /// Yüzde-değişim alarmı için eşik (örn. 5 = %5). Sadece <see cref="AlertType.PercentChange"/>'de kullanılır.
    /// </summary>
    public decimal? PercentChangeThreshold { get; set; }

    /// <summary>
    /// Yüzde-değişim alarmının referans fiyatı — alarm kurulduğu andaki fiyat.
    /// Yüzde sapma bu değere göre hesaplanır.
    /// </summary>
    public decimal? ReferencePrice { get; set; }

    /// <summary>
    /// Legacy flag kept for API backward compatibility.
    /// Persistent monitoring writes <see cref="AlertSignal"/> rows instead;
    /// this property must NOT suppress future signals. Prefer <see cref="IsActive"/>.
    /// </summary>
    public bool IsTriggered { get; set; }

    public bool IsActive { get; set; } = true;
    public AlertInterval Interval { get; set; } = AlertInterval.Minute;

    /// <summary>
    /// UTC timestamp of the last monitoring evaluation for this alert.
    /// Null means the alert has never been checked and is due on the next cycle (Option A).
    /// </summary>
    public DateTime? LastCheckedAt { get; set; }

    /// <summary>
    /// Bu alarm için en son e-posta bildiriminin gönderildiği UTC zaman.
    /// Spam önleme (cooldown) için kullanılır; null ise henüz bildirim gönderilmemiştir.
    /// </summary>
    public DateTime? LastEmailNotifiedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<AlertSignal> Signals { get; set; } = new List<AlertSignal>();
}
