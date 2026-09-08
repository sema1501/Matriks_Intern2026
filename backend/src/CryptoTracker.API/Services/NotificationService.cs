using CryptoTracker.API.Data;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CryptoTracker.API.Services;

/// <summary>
/// Bildirim ayarları. <see cref="CooldownMinutes"/>, aynı alarm/bot için ardışık
/// e-postalar arasındaki minimum süredir (spam önleme). Varsayılan 60 dakika.
/// </summary>
public class NotificationOptions
{
    public const string SectionName = "Notifications";

    public int CooldownMinutes { get; set; } = 60;
}

public sealed class NotificationService(
    AppDbContext db,
    IEmailSender emailSender,
    IOptions<NotificationOptions> options,
    ILogger<NotificationService> logger) : INotificationService
{
    private TimeSpan Cooldown => TimeSpan.FromMinutes(Math.Max(0, options.Value.CooldownMinutes));

    public async Task NotifyPriceAlertAsync(
        PriceAlert alert,
        decimal price,
        DateTime triggeredAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Id == alert.UserId, cancellationToken);

            if (!ShouldNotify(user, alert.LastEmailNotifiedAt, triggeredAt))
                return;

            var direction = alert.Direction == AlertDirection.Above ? "üstüne çıktı" : "altına düştü";
            var subject = $"[CryptoTracker] {alert.Symbol} fiyat alarmı tetiklendi";
            var body =
                $"Merhaba {user!.Username},\n\n" +
                $"{alert.Symbol} için kurduğun fiyat alarmı tetiklendi.\n" +
                $"Hedef fiyat {alert.TargetPrice:0.########} seviyesinin {direction}.\n" +
                $"Tetiklenme anındaki fiyat: {price:0.########}\n" +
                $"Zaman (UTC): {triggeredAt:yyyy-MM-dd HH:mm:ss}\n\n" +
                "Bu bildirimleri profil sayfandan kapatabilirsin.\n";

            await emailSender.SendAsync(user.Email, subject, body, cancellationToken);

            alert.LastEmailNotifiedAt = triggeredAt;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Bildirim hatası izleme döngüsünü durdurmamalı.
            logger.LogError(ex, "Alarm {AlertId} için e-posta bildirimi gönderilemedi", alert.Id);
        }
    }

    public async Task NotifyBotSignalAsync(
        TradingBot bot,
        BotSignalType signalType,
        decimal price,
        decimal rsi,
        DateTime triggeredAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Id == bot.UserId, cancellationToken);

            // Cooldown'ı kalıcı yazabilmek için takip edilen (tracked) bot örneğini yükle.
            var trackedBot = await db.TradingBots
                .FirstOrDefaultAsync(b => b.Id == bot.Id, cancellationToken);

            if (trackedBot is null || !ShouldNotify(user, trackedBot.LastEmailNotifiedAt, triggeredAt))
                return;

            var action = signalType == BotSignalType.Buy ? "ALIM" : "SATIM";
            var subject = $"[CryptoTracker] {bot.Symbol} botu {action} sinyali üretti";
            var body =
                $"Merhaba {user!.Username},\n\n" +
                $"{bot.Symbol} için çalışan botun bir {action} sinyali üretti ve işlem gerçekleştirildi.\n" +
                $"Sinyal anındaki fiyat: {price:0.########}\n" +
                $"RSI: {rsi:0.##}\n" +
                $"Zaman (UTC): {triggeredAt:yyyy-MM-dd HH:mm:ss}\n\n" +
                "Bu bildirimleri profil sayfandan kapatabilirsin.\n";

            await emailSender.SendAsync(user.Email, subject, body, cancellationToken);

            trackedBot.LastEmailNotifiedAt = triggeredAt;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bot {BotId} için e-posta bildirimi gönderilemedi", bot.Id);
        }
    }

    /// <summary>
    /// Kullanıcı var, e-posta tercihi açık ve son bildirimden bu yana cooldown geçmişse true.
    /// </summary>
    private bool ShouldNotify(User? user, DateTime? lastNotifiedAt, DateTime now)
    {
        if (user is null || !user.EmailNotificationsEnabled || string.IsNullOrWhiteSpace(user.Email))
            return false;

        if (lastNotifiedAt is not null && now - lastNotifiedAt.Value < Cooldown)
            return false;

        return true;
    }
}
