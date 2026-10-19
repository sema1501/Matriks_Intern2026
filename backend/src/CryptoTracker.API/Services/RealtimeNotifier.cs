using CryptoTracker.API.DTOs;
using CryptoTracker.API.Hubs;
using CryptoTracker.API.Models;
using Microsoft.AspNetCore.SignalR;

namespace CryptoTracker.API.Services;

/// <summary>Frontend'in dinlediği olay adları. Değiştirilirse RealtimeContext de güncellenmeli.</summary>
public static class RealtimeEvents
{
    public const string AlertTriggered = "alertTriggered";
    public const string BotSignal = "botSignal";
    public const string BotSignalPending = "botSignalPending";
}

/// <summary>alertTriggered olayının içeriği. Id, polling'deki AlertSignalDto.Id ile aynıdır (tekilleştirme için).</summary>
public record AlertTriggeredEvent(int Id, int AlertId, string Symbol, decimal PriceAtTrigger, DateTime TriggeredAt);

/// <summary>
/// İş servisleri hub'a doğrudan değil bu arayüze bağımlıdır; testlerde sahtesi kullanılır.
/// </summary>
public interface IRealtimeNotifier
{
    Task SendToUserAsync(int userId, string eventName, object payload, CancellationToken cancellationToken = default);
}

public static class RealtimeNotifierExtensions
{
    public static Task SendAlertTriggeredAsync(
        this IRealtimeNotifier notifier, PriceAlert alert, AlertSignal signal, CancellationToken cancellationToken = default) =>
        notifier.SendToUserAsync(
            alert.UserId,
            RealtimeEvents.AlertTriggered,
            new AlertTriggeredEvent(signal.Id, alert.Id, alert.Symbol, signal.PriceAtTrigger, signal.TriggeredAt),
            cancellationToken);

    /// <summary>Onay bekleyen sinyal ayrı olayla, diğerleri (gerçekleşen / başarısız) botSignal ile gider.</summary>
    public static Task SendBotSignalAsync(
        this IRealtimeNotifier notifier, TradingBot bot, BotSignal signal, CancellationToken cancellationToken = default) =>
        notifier.SendToUserAsync(
            bot.UserId,
            signal.Status == BotSignalStatus.Pending ? RealtimeEvents.BotSignalPending : RealtimeEvents.BotSignal,
            new BotSignalResponse(
                signal.Id, signal.BotId, signal.SignalType, signal.RsiValueAtSignal,
                signal.PriceAtSignal, signal.CreatedAt, signal.Status, signal.Reason),
            cancellationToken);
}

public class SignalRRealtimeNotifier(
    IHubContext<NotificationHub> hubContext,
    ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
{
    public async Task SendToUserAsync(int userId, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        try
        {
            await hubContext.Clients.User(userId.ToString()).SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Anlık bildirim ek bir kanal: gönderilemezse kayıt ve e-posta yine yapılmış olur,
            // frontend de polling ile yakalar. İzleme döngüsü bu yüzden durmamalı.
            logger.LogWarning(ex, "Realtime event {EventName} could not be sent to user {UserId}", eventName, userId);
        }
    }
}
