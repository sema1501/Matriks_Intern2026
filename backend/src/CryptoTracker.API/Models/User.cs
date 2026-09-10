namespace CryptoTracker.API.Models;
public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    /// <summary>Sanal USD bakiyesi. Yeni kullanıcılar 10.000 ile başlar.</summary>
    public decimal VirtualBalance { get; set; } = 10_000m;

    /// <summary>
    /// Alarm/bot sinyali tetiklendiğinde kullanıcıya e-posta bildirimi gönderilsin mi?
    /// Varsayılan açık; kullanıcı profilinden kapatabilir (Görev 40).
    /// </summary>
    public bool EmailNotificationsEnabled { get; set; } = true;

    /// <summary>
    /// Kullanıcının e-posta adresi doğrulandı mı? Yeni kayıtlar false başlar ve
    /// doğrulama linkine tıklayana kadar giriş yapamaz (Görev 48).
    /// Migration mevcut kullanıcıları true işaretler (geriye uyumluluk).
    /// </summary>
    public bool EmailConfirmed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<PriceAlert> PriceAlerts { get; set; } = new List<PriceAlert>();
    public ICollection<PortfolioHolding> Holdings { get; set; } = new List<PortfolioHolding>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<TradingBot> TradingBots { get; set; } = new List<TradingBot>();
}
