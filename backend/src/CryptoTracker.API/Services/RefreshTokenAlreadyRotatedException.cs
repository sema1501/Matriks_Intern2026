namespace CryptoTracker.API.Services;

/// <summary>
/// Refresh token birkaç saniye önce başka bir istekle (çoğunlukla başka bir sekme) yenilenmiş.
/// 401 döner ama oturum kapatılmaz; istemci diğer sekmenin kaydettiği yeni token'ı kullanabilir.
/// </summary>
public sealed class RefreshTokenAlreadyRotatedException()
    : UnauthorizedAccessException("Oturum başka bir sekmede yenilendi.")
{
    /// <summary>Hata yanıtındaki <c>code</c> alanı; frontend bu değere bakar.</summary>
    public const string ErrorCode = "refresh_token_rotated";
}
