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
    private const string StrategyName = "RSI";

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
            StrategyName,
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

        var signals = new List<BacktestSignalDto>();
        var quantity = bot.TradeQuantity;

        decimal? openEntryPrice = null;
        decimal realizedPnL = 0m;
        decimal totalEntryValue = 0m;
        var completedTrades = 0;
        var winningTrades = 0;
        var losingTrades = 0;

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

            var typeLabel = signalType == BotSignalType.Buy ? "BUY" : "SELL";

            // Signal list = strategy events (independent of position state).
            signals.Add(new BacktestSignalDto(
                candle.OpenTimeUtc,
                typeLabel,
                candle.ClosePrice,
                rsi.Value));

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
