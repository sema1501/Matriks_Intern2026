using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CryptoTracker.API.RateLimiting;

/// <summary>
/// Rate limit ayarları (Görev 88). appsettings.json içindeki "RateLimiting" bölümünden okunur.
/// </summary>
public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Kimlik doğrulama uçları: IP başına pencere içindeki izin verilen istek sayısı.</summary>
    public int AuthPermitLimit { get; set; } = 5;

    /// <summary>Kimlik doğrulama uçları için pencere süresi (saniye).</summary>
    public int AuthWindowSeconds { get; set; } = 60;

    /// <summary>Diğer tüm uçlar: IP başına pencere içindeki izin verilen istek sayısı (gevşek limit).</summary>
    public int GeneralPermitLimit { get; set; } = 300;

    /// <summary>Diğer uçlar için pencere süresi (saniye).</summary>
    public int GeneralWindowSeconds { get; set; } = 60;
}

public static class RateLimitingSetup
{
    /// <summary>Login, register, forgot-password ve confirm-email uçlarına uygulanan sıkı politika.</summary>
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
                      ?? new RateLimitingOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // 1) Sıkı politika: kimlik doğrulama uçları, IP başına sabit pencere.
            limiter.AddPolicy(AuthPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIp(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(options.AuthWindowSeconds),
                        QueueLimit = 0
                    }));

            // 2) Gevşek politika: diğer tüm uçlar, IP başına yüksek limit.
            //    Kimlik doğrulama uçları sıkı politikayla ayrıca sınırlanır.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIp(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.GeneralPermitLimit,
                        Window = TimeSpan.FromSeconds(options.GeneralWindowSeconds),
                        QueueLimit = 0
                    }));

            // Limit aşıldığında 429 + anlamlı mesaj + Retry-After başlığı.
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfterSeconds = options.AuthWindowSeconds;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);

                context.HttpContext.Response.Headers.RetryAfter =
                    retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = $"Çok fazla deneme yaptınız. Lütfen {retryAfterSeconds} saniye sonra tekrar deneyin.",
                    retryAfterSeconds
                }, cancellationToken);
            };
        });

        return services;
    }

    private static string GetClientIp(HttpContext httpContext)
        => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
