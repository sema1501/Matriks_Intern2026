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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<AlertSignal> Signals { get; set; } = new List<AlertSignal>();
}
