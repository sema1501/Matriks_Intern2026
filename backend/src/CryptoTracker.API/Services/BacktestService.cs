using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public interface IBacktestService
{
    Task<BacktestResponseDto> RunAsync(
        int userId,
        int botId,
        BacktestRequestDto request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Pure historical simulation. Does not execute trades, write BotSignals,
/// touch PortfolioService, or call any order endpoints.
/// Constructor depends only on AppDbContext (read) and IBinanceKlineService (public market data).
/// </summary>
public sealed class BacktestService(
    AppDbContext db,
    IBinanceKlineService klineService) : IBacktestService
{
    // EndDate more than this far ahead of UtcNow is rejected.
    private static readonly TimeSpan MaxFutureSkew = TimeSpan.FromHours(24);

    public async Task<BacktestResponseDto> RunAsync(
        int userId,
        int botId,
        BacktestRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateDateRange(request.StartDate, request.EndDate);

        var bot = await db.TradingBots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.Id == botId && b.UserId == userId,
                cancellationToken);

        if (bot is null)
            throw new KeyNotFoundException("Bot bulunamadı.");

        if (bot.TradeQuantity <= 0)
            throw new ArgumentException("Bot trade quantity must be greater than zero.");

        var startUtc = EnsureUtc(request.StartDate);
        var endUtc = EnsureUtc(request.EndDate);

        // #81: strateji artık sabit "RSI" değil, botun kendisinden okunur.
        if (bot.Strategy == BotStrategy.EmaCrossover)
            return await RunEmaAsync(bot, startUtc, endUtc, cancellationToken);

        // Warm-up candles before StartDate so RSI is valid from the first in-range bar.
        var warmUpStart = startUtc.AddMinutes(-RsiSignalEvaluator.Period);

        var candles = await klineService.GetHistoricalKlinesAsync(
            bot.Symbol,
            RsiSignalEvaluator.Interval,
            warmUpStart,
            endUtc,
            cancellationToken);

        if (candles.Count == 0)
        {
            throw new InvalidOperationException(
                "Seçilen tarih aralığı için Binance'ten tarihsel mum verisi alınamadı.");
        }

        var closes = candles.Select(c => c.ClosePrice).ToList();
        var rsiSeries = RsiCalculator.CalculateSeries(closes, RsiSignalEvaluator.Period);

        var inRangeCount = candles.Count(c =>
            c.OpenTimeUtc >= startUtc && c.OpenTimeUtc <= endUtc);

        if (inRangeCount == 0)
        {
            throw new InvalidOperationException(
                "Seçilen tarih aralığında hiç mum bulunamadı.");
        }

        var hasAnyRsiInRange = false;
        for (var i = 0; i < candles.Count; i++)
        {
            if (candles[i].OpenTimeUtc < startUtc || candles[i].OpenTimeUtc > endUtc)
                continue;

            if (rsiSeries[i] is not null)
            {
                hasAnyRsiInRange = true;
                break;
            }
        }

        if (!hasAnyRsiInRange)
        {
            throw new InvalidOperationException(
                $"RSI hesaplamak için yetersiz mum geçmişi. En az {RsiSignalEvaluator.Period + 1} kapanış fiyatı gereklidir.");
        }

        var simulation = Simulate(bot, candles, rsiSeries, startUtc, endUtc);

        return new BacktestResponseDto(
            bot.Id,
            bot.Symbol,
            StrategyLabel(bot.Strategy),
            startUtc,
            endUtc,
            RsiSignalEvaluator.Interval,
            simulation.Summary,
            simulation.Signals);
    }

    internal static SimulationResult Simulate(
        TradingBot bot,
        IReadOnlyList<BinanceKlineCandle> candles,
        IReadOnlyList<decimal?> rsiSeries,
        DateTime startUtc,
        DateTime endUtc)
    {
        if (bot.TradeQuantity <= 0)
            throw new ArgumentException("Bot trade quantity must be greater than zero.");

        var events = new List<(BinanceKlineCandle Candle, BotSignalType Type, decimal? Rsi)>();

        // Previous RSI among in-range bars only (warm-up never seeds zone-entry or positions).
        decimal? previousInRangeRsi = null;

        for (var i = 0; i < candles.Count; i++)
        {
            var candle = candles[i];

            if (candle.OpenTimeUtc < startUtc || candle.OpenTimeUtc > endUtc)
                continue;

            var rsi = rsiSeries[i];
            if (rsi is null)
                continue;

            var signalType = RsiSignalEvaluator.DetermineZoneEntrySignal(
                rsi.Value,
                previousInRangeRsi,
                bot.BuyRsiThreshold,
                bot.SellRsiThreshold);

            previousInRangeRsi = rsi.Value;

            if (signalType is null)
                continue;

            events.Add((candle, signalType.Value, rsi.Value));
        }

        return BuildResult(bot, candles, events, startUtc, endUtc);
    }

    /// <summary>
    /// #81: EMA kesişim backtest'i. Canlı bot (BotMonitorService) ile aynı
    /// EmaCalculator + EmaCrossoverEvaluator ikilisini kullanır; böylece
    /// backtest ile canlı davranış birebir aynı kurala göre sinyal üretir.
    /// </summary>
    internal static SimulationResult SimulateEma(
        TradingBot bot,
        IReadOnlyList<BinanceKlineCandle> candles,
        DateTime startUtc,
        DateTime endUtc)
    {
        if (bot.TradeQuantity <= 0)
            throw new ArgumentException("Bot trade quantity must be greater than zero.");

        if (bot.ShortEmaPeriod is null || bot.LongEmaPeriod is null)
            throw new ArgumentException("EMA stratejisi için kısa ve uzun periyot zorunlu.");

        var closes = candles.Select(c => c.ClosePrice).ToList();
        var shortEma = EmaCalculator.CalculateSeries(closes, bot.ShortEmaPeriod.Value);
        var longEma = EmaCalculator.CalculateSeries(closes, bot.LongEmaPeriod.Value);

        var events = new List<(BinanceKlineCandle Candle, BotSignalType Type, decimal? Rsi)>();
        var hasPreviousInRange = false;

        for (var i = 0; i < candles.Count; i++)
        {
            var candle = candles[i];

            if (candle.OpenTimeUtc < startUtc || candle.OpenTimeUtc > endUtc)
                continue;

            // İlk aralık-içi mum yalnızca "önceki" değer olur; ısınma mumları sinyal üretmez
            // (RSI tarafındaki previousInRangeRsi mantığıyla aynı).
            if (!hasPreviousInRange)
            {
                hasPreviousInRange = shortEma[i] is not null && longEma[i] is not null;
                continue;
            }

            var signalType = EmaCrossoverEvaluator.DetermineCrossoverSignal(
                shortEma[i - 1],
                longEma[i - 1],
                shortEma[i],
                longEma[i]);

            if (signalType is null)
                continue;

            // EMA sinyallerinde RSI kullanılmaz; Rsi alanı null döner.
            events.Add((candle, signalType.Value, null));
        }

        return BuildResult(bot, candles, events, startUtc, endUtc);
    }

    /// <summary>
    /// Sinyal listesinden özet (PnL, kazanan/kaybeden işlem) üretir.
    /// RSI ve EMA simülasyonları aynı pozisyon mantığını paylaşır: long-only,
    /// açık pozisyon varken tekrar BUY yok sayılır, aralık sonunda açık pozisyon gerçekleşmemiş kalır.
    /// </summary>
    private static SimulationResult BuildResult(
        TradingBot bot,
        IReadOnlyList<BinanceKlineCandle> candles,
        IReadOnlyList<(BinanceKlineCandle Candle, BotSignalType Type, decimal? Rsi)> events,
        DateTime startUtc,
        DateTime endUtc)
    {
        var signals = new List<BacktestSignalDto>();
        var quantity = bot.TradeQuantity;

        decimal? openEntryPrice = null;
        decimal realizedPnL = 0m;
        decimal totalEntryValue = 0m;
        var completedTrades = 0;
        var winningTrades = 0;
        var losingTrades = 0;

        foreach (var (candle, signalType, rsi) in events)
        {
            var typeLabel = signalType == BotSignalType.Buy ? "BUY" : "SELL";

            // Signal list = strategy events (independent of position state).
            signals.Add(new BacktestSignalDto(
                candle.OpenTimeUtc,
                typeLabel,
                candle.ClosePrice,
                rsi));

            if (signalType == BotSignalType.Buy)
            {
                // Long-only: ignore duplicate BUY while already in a position.
                if (openEntryPrice is null)
                {
                    openEntryPrice = candle.ClosePrice;
                    totalEntryValue += candle.ClosePrice * quantity;
                }
            }
            else
            {
                // SELL with no open position: keep signal, no PnL impact.
                if (openEntryPrice is not null)
                {
                    var tradePnL = (candle.ClosePrice - openEntryPrice.Value) * quantity;
                    realizedPnL += tradePnL;
                    completedTrades++;

                    if (tradePnL > 0)
                        winningTrades++;
                    else if (tradePnL < 0)
                        losingTrades++;

                    openEntryPrice = null;
                }
            }
        }

        // End-of-range: leave open position unrealized; mark-to-market at final in-range close.
        decimal unrealizedPnL = 0m;
        if (openEntryPrice is not null)
        {
            var finalClose = FindLastInRangeClose(candles, startUtc, endUtc);
            if (finalClose is not null)
                unrealizedPnL = (finalClose.Value - openEntryPrice.Value) * quantity;
        }

        var netPnL = realizedPnL + unrealizedPnL;
        var realizedReturnPercentage = totalEntryValue == 0
            ? 0m
            : Math.Round((realizedPnL / totalEntryValue) * 100m, 4);

        var buySignals = signals.Count(s => s.Type == "BUY");
        var sellSignals = signals.Count(s => s.Type == "SELL");

        var summary = new BacktestSummaryDto(
            signals.Count,
            buySignals,
            sellSignals,
            completedTrades,
            winningTrades,
            losingTrades,
            Math.Round(realizedPnL, 8),
            Math.Round(unrealizedPnL, 8),
            Math.Round(netPnL, 8),
            realizedReturnPercentage);

        return new SimulationResult(summary, signals);
    }

    private async Task<BacktestResponseDto> RunEmaAsync(
        TradingBot bot,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        if (bot.ShortEmaPeriod is null || bot.LongEmaPeriod is null)
            throw new ArgumentException("EMA stratejisi için kısa ve uzun periyot zorunlu.");

        // Uzun EMA'nın ilk değeri için LongEmaPeriod kadar ısınma mumu + canlı taraftaki ek ısınma.
        var warmUpCandles = EmaCrossoverEvaluator.RequiredCandleCount(bot.LongEmaPeriod.Value)
                            + EmaCrossoverEvaluator.SeedWarmupCandles;
        var warmUpStart = startUtc.AddMinutes(-warmUpCandles);

        var candles = await klineService.GetHistoricalKlinesAsync(
            bot.Symbol,
            EmaCrossoverEvaluator.Interval,
            warmUpStart,
            endUtc,
            cancellationToken);

        if (candles.Count == 0)
        {
            throw new InvalidOperationException(
                "Seçilen tarih aralığı için Binance'ten tarihsel mum verisi alınamadı.");
        }

        if (!candles.Any(c => c.OpenTimeUtc >= startUtc && c.OpenTimeUtc <= endUtc))
        {
            throw new InvalidOperationException(
                "Seçilen tarih aralığında hiç mum bulunamadı.");
        }

        if (candles.Count < EmaCrossoverEvaluator.RequiredCandleCount(bot.LongEmaPeriod.Value))
        {
            throw new InvalidOperationException(
                $"EMA hesaplamak için yetersiz mum geçmişi. En az {EmaCrossoverEvaluator.RequiredCandleCount(bot.LongEmaPeriod.Value)} kapanış fiyatı gereklidir.");
        }

        var simulation = SimulateEma(bot, candles, startUtc, endUtc);

        return new BacktestResponseDto(
            bot.Id,
            bot.Symbol,
            StrategyLabel(bot.Strategy),
            startUtc,
            endUtc,
            EmaCrossoverEvaluator.Interval,
            simulation.Summary,
            simulation.Signals);
    }

    internal static string StrategyLabel(BotStrategy strategy) =>
        strategy == BotStrategy.EmaCrossover ? "EMA" : "RSI";

    internal static void ValidateDateRange(DateTime startDate, DateTime endDate)
    {
        var startUtc = EnsureUtc(startDate);
        var endUtc = EnsureUtc(endDate);

        if (startUtc == endUtc)
            throw new ArgumentException("Başlangıç ve bitiş tarihleri aynı olamaz.");

        if (startUtc >= endUtc)
            throw new ArgumentException("Başlangıç tarihi bitiş tarihinden önce olmalıdır.");

        var maxEnd = DateTime.UtcNow.Add(MaxFutureSkew);
        if (endUtc > maxEnd)
            throw new ArgumentException("Bitiş tarihi gelecekte çok ileride olamaz.");
    }

    private static decimal? FindLastInRangeClose(
        IReadOnlyList<BinanceKlineCandle> candles,
        DateTime startUtc,
        DateTime endUtc)
    {
        for (var i = candles.Count - 1; i >= 0; i--)
        {
            if (candles[i].OpenTimeUtc >= startUtc && candles[i].OpenTimeUtc <= endUtc)
                return candles[i].ClosePrice;
        }

        return null;
    }

    /// <summary>
    /// Accepts UTC and Local (converted). Rejects Unspecified to avoid silent misinterpretation.
    /// </summary>
    internal static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => throw new ArgumentException(
                "Date values must have an explicit Kind (UTC preferred). DateTimeKind.Unspecified is not allowed.")
        };

    internal sealed record SimulationResult(
        BacktestSummaryDto Summary,
        IReadOnlyList<BacktestSignalDto> Signals);
}
