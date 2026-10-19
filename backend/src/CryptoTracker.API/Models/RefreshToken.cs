namespace CryptoTracker.API.Models;

/// <summary>
/// Uzun ömürlü oturum yenileme token'ı.
/// Token'ın kendisi değil, yalnızca SHA-256 hash'i saklanır: veritabanı sızsa bile
/// buradaki değerlerle oturum açılamaz. (PasswordResetToken token'ı düz metin saklıyor,
/// bu sınıf o deseni bilerek izlemiyor.)
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Token'ın SHA-256 hash'i (64 karakter hex).</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Doluysa token iptal edilmiştir (kullanıldı, çıkış yapıldı ya da şifre değişti).</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Token rotation: bu token kullanılınca yerine üretilen yeni token'ın hash'i.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}
