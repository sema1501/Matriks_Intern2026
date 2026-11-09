using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;

namespace CryptoTracker.API.Services;

/// <summary>Backtest'te kapanan tek bir işlem.</summary>
public sealed record BacktestTrade(
    decimal EntryPrice,
    decimal ExitPrice,
    decimal Quantity,
    BotSignalReason Reason)
{
    public decimal ProfitLoss => (ExitPrice - EntryPrice) * Quantity;
    public decimal ReturnPercent => (ExitPrice - EntryPrice) / EntryPrice * 100m;
}

/// <summary>
/// Backtest risk metrikleri. Veritabanına ya da Binance'e bağlı değildir: işlem listesi ve mum başına
/// portföy değerinden sonuç üretir, bu yüzden elle hesaplanmış örneklerle doğrudan test edilebilir.
/// Hesaplanamayan değer (örn. hiç işlem yoksa kazanma oranı) sıfır değil null döner.
/// </summary>
public static class BacktestMetrics
{
    /// <summary>API'nin döndüğü değer eğrisindeki en fazla nokta sayısı.</summary>
    public const int MaxCurvePoints = 300;

    private const int PercentDigits = 4;

    public static BacktestMetricsDto Calculate(
        IReadOnlyList<BacktestTrade> trades,
        IReadOnlyList<BacktestEquityPointDto> equityCurve,
        decimal? firstPrice,
        decimal? lastPrice)
    {
        var strategyReturn = CalculateReturnPercent(equityCurve.FirstOrDefault()?.Value, equityCurve.LastOrDefault()?.Value);
        var buyAndHoldReturn = CalculateReturnPercent(firstPrice, lastPrice);

        return new BacktestMetricsDto(
            CalculateMaxDrawdownPercent(equityCurve),
            CalculateWinRatePercent(trades),
            CalculateProfitFactor(trades),
            CalculateAverageTradeReturnPercent(trades),
            trades.Count(t => t.Reason == BotSignalReason.StopLoss),
            trades.Count(t => t.Reason == BotSignalReason.TakeProfit),
            strategyReturn,
            buyAndHoldReturn,
            strategyReturn - buyAndHoldReturn,
            Downsample(equityCurve, MaxCurvePoints));
    }

    /// <summary>Görülen en yüksek değerden sonraki en düşük değere yaşanan en büyük yüzde kayıp.</summary>
    public static decimal? CalculateMaxDrawdownPercent(IReadOnlyList<BacktestEquityPointDto> equityCurve)
    {
        if (equityCurve.Count == 0)
            return null;

        var peak = equityCurve[0].Value;
        var maxDrawdown = 0m;

        foreach (var point in equityCurve)
        {
            if (point.Value > peak)
                peak = point.Value;
            else if (peak > 0)
                maxDrawdown = Math.Max(maxDrawdown, (peak - point.Value) / peak * 100m);
        }

        return Math.Round(maxDrawdown, PercentDigits);
    }

    public static decimal? CalculateWinRatePercent(IReadOnlyList<BacktestTrade> trades)
        => trades.Count == 0
            ? null
            : Math.Round((decimal)trades.Count(t => t.ProfitLoss > 0) / trades.Count * 100m, PercentDigits);

    /// <summary>Toplam kâr ÷ toplam zarar. Hiç zarar yoksa oran tanımsız olduğu için null.</summary>
    public static decimal? CalculateProfitFactor(IReadOnlyList<BacktestTrade> trades)
    {
        var totalLoss = -trades.Where(t => t.ProfitLoss < 0).Sum(t => t.ProfitLoss);
        if (totalLoss == 0)
            return null;

        var totalProfit = trades.Where(t => t.ProfitLoss > 0).Sum(t => t.ProfitLoss);
        return Math.Round(totalProfit / totalLoss, PercentDigits);
    }

    public static decimal? CalculateAverageTradeReturnPercent(IReadOnlyList<BacktestTrade> trades)
        => trades.Count == 0
            ? null
            : Math.Round(trades.Average(t => t.ReturnPercent), PercentDigits);

    /// <summary>
    /// Eğriyi eşit aralıkla seyreltir. İlk, son ve en düşük nokta her zaman kalır; böylece
    /// grafikte başlangıç, bitiş ve en kötü an kaybolmaz.
    /// </summary>
    public static IReadOnlyList<BacktestEquityPointDto> Downsample(
        IReadOnlyList<BacktestEquityPointDto> points,
        int maxPoints)
    {
        if (points.Count <= maxPoints)
            return points;

        var lowestIndex = 0;
        for (var i = 1; i < points.Count; i++)
        {
            if (points[i].Value < points[lowestIndex].Value)
                lowestIndex = i;
        }

        var lastIndex = points.Count - 1;
        var indexes = new SortedSet<int> { 0, lowestIndex, lastIndex };

        var evenSlots = maxPoints - indexes.Count;
        for (var slot = 1; slot <= evenSlots; slot++)
            indexes.Add((int)((long)slot * lastIndex / (evenSlots + 1)));

        return indexes.Select(i => points[i]).ToList();
    }

    private static decimal? CalculateReturnPercent(decimal? start, decimal? end)
        => start is > 0 && end is not null
            ? Math.Round((end.Value - start.Value) / start.Value * 100m, PercentDigits)
            : null;
}
