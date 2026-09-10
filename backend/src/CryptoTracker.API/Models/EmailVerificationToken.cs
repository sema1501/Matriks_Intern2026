namespace CryptoTracker.API.Models;

/// <summary>
/// Kayıt sırasında üretilen e-posta doğrulama token'ı (Görev 48).
/// <see cref="PasswordResetToken"/> ile aynı deseni izler.
/// </summary>
public class EmailVerificationToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
}
