namespace CryptoTracker.API.Services;

/// <summary>
/// Refresh token'ın taşındığı cookie. HttpOnly olduğu için sayfadaki JavaScript okuyamaz, XSS ile çalınamaz.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "refreshToken";

    private const string CookiePath = "/api/Auth";
    private const int DefaultRefreshTokenDays = 7;

    public static string Read(HttpRequest request) => request.Cookies[Name] ?? string.Empty;

    public static void Write(HttpResponse response, string refreshToken, IConfiguration configuration)
    {
        var days = configuration.GetValue("Jwt:RefreshTokenDays", DefaultRefreshTokenDays);
        var options = BuildOptions(configuration);
        options.MaxAge = TimeSpan.FromDays(days);
        response.Cookies.Append(Name, refreshToken, options);
    }

    public static void Delete(HttpResponse response, IConfiguration configuration)
        => response.Cookies.Delete(Name, BuildOptions(configuration));

    private static CookieOptions BuildOptions(IConfiguration configuration) => new()
    {
        HttpOnly = true,
        // Bu cookie yalnızca kendi sayfamızın yaptığı refresh ve logout isteklerinde gerekiyor, başka bir
        // siteden gelen bağlantıyla açılışta gitmesine ihtiyaç yok. Bu yüzden Lax yerine en sıkı seçenek.
        SameSite = SameSiteMode.Strict,
        // Yalnızca oturum uçlarına gider, her API isteğine eklenmez.
        Path = CookiePath,
        // Ayar yoksa açık: üretimde unutulsa bile token http üzerinden gitmez.
        // Yerelde http için launchSettings.json kapatıyor.
        Secure = configuration.GetValue("Auth:RefreshCookieSecure", true)
    };
}
