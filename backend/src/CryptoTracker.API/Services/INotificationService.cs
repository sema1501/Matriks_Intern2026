using CryptoTracker.API.Models;

namespace CryptoTracker.API.Services;

/// <summary>
/// Alarm/bot sinyalleri için e-posta bildirimi orkestrasyonu (Görev 40).
/// Kullanıcı tercihini ve spam önleme cooldown'ını tek yerde yönetir.
/// </summary>
public interface INotificationService
{
    Task NotifyPriceAlertAsync(PriceAlert alert, decimal price, DateTime triggeredAt, CancellationToken cancellationToken = default);

    Task NotifyBotSignalAsync(TradingBot bot, BotSignalType signalType, decimal price, decimal rsi, DateTime triggeredAt, CancellationToken cancellationToken = default, BotSignalReason reason = BotSignalReason.Strategy);

    /// <summary>Stop-loss / take-profit satışı tüm denemelere rağmen yapılamadı (#100). Pozisyon başına bir kez çağrılır.</summary>
    Task NotifyRiskExitFailedAsync(TradingBot bot, BotSignalReason reason, decimal price, int attempts, DateTime failedAt, CancellationToken cancellationToken = default);
}
