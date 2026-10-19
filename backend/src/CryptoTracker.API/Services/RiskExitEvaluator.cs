using CryptoTracker.API.Models;

namespace CryptoTracker.API.Services;

/// <summary>
/// Stop-loss / take-profit kararı. Veritabanına dokunmaz; RsiSignalEvaluator ve
/// EmaCrossoverEvaluator gibi yalnızca verilen sayılardan karar üretir.
/// Canlı bot (BotMonitorService) ve backtest (BacktestService) aynı kuralı kullanır.
/// </summary>
public static class RiskExitEvaluator
{
    /// <summary>
    /// Açık pozisyon için çıkış nedeni döner; çıkış gerekmiyorsa null.
    /// Sınır dahildir: %5 stop-loss'ta fiyat tam %5 düştüğünde satılır.
    /// İkisi aynı anda sağlanamaz (fiyat hem düşüp hem yükselemez), yine de önce stop-loss bakılır.
    /// </summary>
    public static BotSignalReason? Evaluate(
        decimal entryPrice,
        decimal currentPrice,
        decimal? stopLossPercent,
        decimal? takeProfitPercent)
    {
        if (entryPrice <= 0m || currentPrice <= 0m)
            return null;

        if (stopLossPercent is > 0m)
        {
            var stopPrice = entryPrice * (1m - stopLossPercent.Value / 100m);
            if (currentPrice <= stopPrice)
                return BotSignalReason.StopLoss;
        }

        if (takeProfitPercent is > 0m)
        {
            var targetPrice = entryPrice * (1m + takeProfitPercent.Value / 100m);
            if (currentPrice >= targetPrice)
                return BotSignalReason.TakeProfit;
        }

        return null;
    }

    /// <summary>
    /// Botun açık pozisyonunun giriş fiyatı: son başarılı (Approved) sinyal AL ise onun fiyatı.
    /// Son başarılı sinyal SAT ise ya da hiç başarılı sinyal yoksa açık pozisyon yoktur (null).
    /// </summary>
    public static decimal? FindOpenPositionEntryPrice(IEnumerable<BotSignal> signals)
    {
        var lastApproved = signals
            .Where(s => s.Status == BotSignalStatus.Approved)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .LastOrDefault();

        return lastApproved?.SignalType == BotSignalType.Buy
            ? lastApproved.PriceAtSignal
            : null;
    }
}
