using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CryptoTracker.API.Middleware;

public static class CorsOrigins
{
    public static string[] GetAllowed(IConfiguration configuration)
        => configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"];
}

/// <summary>
/// Refresh ve logout yalnızca cookie ile çalışır. Tarayıcı cookie'yi kendiliğinden eklediği için başka bir
/// siteden tetiklenen istek de cookie'yi taşıyabilir (CSRF). İki önlem birlikte uygulanır, çünkü her birinin
/// tek başına açığı var:
/// - Origin kontrolü isteğin geldiği siteye bakar ama bu başlık her zaman gelmez (bazı proxy ve gizlilik
///   ayarları siler), gelmediğinde tek başına hiçbir şey engellemez.
/// - Özel başlığı başka bir site CORS izni olmadan gönderemez ama bu koruma CORS ayarına bağlıdır, ayar
///   gevşetilirse ya da tarayıcı eklentisi başlığı eklerse Origin kontrolü devam eder.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RefreshCookieCsrfProtectionAttribute : Attribute, IAuthorizationFilter
{
    public const string RequiredHeader = "X-Requested-With";
    public const string RequiredHeaderValue = "XMLHttpRequest";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();

        var origin = request.Headers.Origin.ToString();
        var originAllowed = string.IsNullOrEmpty(origin)
            || CorsOrigins.GetAllowed(configuration).Contains(origin, StringComparer.OrdinalIgnoreCase);

        var hasRequiredHeader = string.Equals(
            request.Headers[RequiredHeader], RequiredHeaderValue, StringComparison.OrdinalIgnoreCase);

        if (!originAllowed || !hasRequiredHeader)
        {
            context.Result = new ObjectResult(new { error = "İstek kaynağı doğrulanamadı." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
