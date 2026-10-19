using CryptoTracker.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CryptoTracker.API.Services;

public class RefreshTokenCleanupOptions
{
    public const string SectionName = "RefreshTokenCleanup";

    /// <summary>Temizliğin kaç saatte bir çalıştığı. Varsayılan günde bir.</summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>
    /// İptal edilmiş token'lar bu kadar gün daha tutulur. Hemen silinirse eski bir token tekrar
    /// geldiğinde kayıt bulunamaz ve yeniden kullanım tespiti (tüm oturumları kapatma) çalışmaz.
    /// </summary>
    public int RevokedRetentionDays { get; set; } = 30;
}

/// <summary>
/// RefreshToken tablosunun sınırsız büyümesini engeller.
/// PortfolioSnapshotService ile aynı desen: zamanlayıcı burada, iş mantığı test edilebilir processor'da.
/// </summary>
public class RefreshTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<RefreshTokenCleanupOptions> options,
    ILogger<RefreshTokenCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = Math.Max(1, options.Value.IntervalHours);
        logger.LogInformation(
            "RefreshTokenCleanupService started. IntervalHours={IntervalHours}.",
            intervalHours);

        // Uygulama sık yeniden başlatılsa da temizlik hiç çalışmadan kalmasın diye ilk tur hemen.
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        logger.LogInformation("RefreshTokenCleanupService stopping");
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var processor = scope.ServiceProvider.GetRequiredService<IRefreshTokenCleanupProcessor>();

            await processor.CleanupAsync(db, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Refresh token cleanup failed; will retry on next interval");
        }
    }
}

public interface IRefreshTokenCleanupProcessor
{
    /// <summary>Silinen kayıt sayısını döner.</summary>
    Task<int> CleanupAsync(AppDbContext db, CancellationToken cancellationToken);
}

public class RefreshTokenCleanupProcessor(
    IClock clock,
    IOptions<RefreshTokenCleanupOptions> options,
    ILogger<RefreshTokenCleanupProcessor> logger) : IRefreshTokenCleanupProcessor
{
    /// <summary>
    /// Silinenler:
    /// - İptal edilmemiş ama süresi dolmuş token'lar (artık hiçbir işe yaramaz).
    /// - RevokedRetentionDays günden önce iptal edilmiş token'lar.
    /// Aktif token'lar ve yeni iptal edilenler kalır.
    /// </summary>
    public async Task<int> CleanupAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var revokedBefore = now.AddDays(-Math.Max(0, options.Value.RevokedRetentionDays));

        var stale = await db.RefreshTokens
            .Where(t =>
                (t.RevokedAt == null && t.ExpiresAt <= now) ||
                (t.RevokedAt != null && t.RevokedAt < revokedBefore))
            .ToListAsync(cancellationToken);

        if (stale.Count > 0)
        {
            db.RefreshTokens.RemoveRange(stale);
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Refresh token cleanup complete: Deleted={Deleted}", stale.Count);
        return stale.Count;
    }
}
