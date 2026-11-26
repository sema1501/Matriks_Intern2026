using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Xunit;

namespace CryptoTracker.API.Tests;

/// <summary>#106: beklenen değerler elle hesaplanmıştır, hesap yorumlarda yazılıdır.</summary>
public class BacktestMetricsTests
{
    private static readonly DateTime Start = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<BacktestEquityPointDto> Curve(params decimal[] values)
        => values.Select((v, i) => new BacktestEquityPointDto(Start.AddMinutes(i), v)).ToList();

    private static BacktestTrade Trade(decimal entry, decimal exit, BotSignalReason reason = BotSignalReason.Strategy)
        => new(entry, exit, 1m, reason);

    private static BacktestMetricsDto Calculate(IReadOnlyList<BacktestTrade> trades, params decimal[] curve)
        => BacktestMetrics.Calculate(trades, Curve(curve), 100m, 125m);

    // ── Maksimum düşüş ──────────────────────────────────────────

    [Fact]
    public void MaxDrawdown_SteadilyRising_IsZero()
    {
        Assert.Equal(0m, BacktestMetrics.CalculateMaxDrawdownPercent(Curve(100m, 110m, 120m)));
    }

    [Fact]
    public void MaxDrawdown_DropAndRecovery_IsLargestPeakToTroughLoss()
    {
        // 120'den 90'a: 30 / 120 = %25. Sonraki 130'dan 117'ye düşüş yalnızca 13 / 130 = %10.
        Assert.Equal(25m, BacktestMetrics.CalculateMaxDrawdownPercent(Curve(100m, 120m, 90m, 130m, 117m)));
    }

    [Fact]
    public void MaxDrawdown_MeasuredFromPeakNotFromStart()
    {
        // Değer hiç başlangıcın altına inmese de 200'den 150'ye düşüş %25'tir.
        Assert.Equal(25m, BacktestMetrics.CalculateMaxDrawdownPercent(Curve(100m, 200m, 150m)));
    }

    [Fact]
    public void MaxDrawdown_EmptyCurve_IsNull()
    {
        Assert.Null(BacktestMetrics.CalculateMaxDrawdownPercent(Curve()));
    }

    // ── İşlem metrikleri ────────────────────────────────────────

    [Fact]
    public void NoTrades_RatesAreNullAndCountsZero()
    {
        var metrics = Calculate([], 100m, 100m);

        Assert.Null(metrics.WinRatePercent);
        Assert.Null(metrics.ProfitFactor);
        Assert.Null(metrics.AverageTradeReturnPercent);
        Assert.Equal(0, metrics.StopLossTrades);
        Assert.Equal(0, metrics.TakeProfitTrades);
        Assert.Equal(0m, metrics.MaxDrawdownPercent);
    }

    [Fact]
    public void NoLosingTrades_ProfitFactorIsNull()
    {
        var metrics = Calculate([Trade(100m, 110m), Trade(100m, 105m)], 100m, 115m);

        Assert.Null(metrics.ProfitFactor);
        Assert.Equal(100m, metrics.WinRatePercent);
        Assert.Equal(7.5m, metrics.AverageTradeReturnPercent); // (%10 + %5) / 2
    }

    [Fact]
    public void SingleLosingTrade()
    {
        var metrics = Calculate([Trade(100m, 90m, BotSignalReason.StopLoss)], 100m, 90m);

        Assert.Equal(0m, metrics.WinRatePercent);
        Assert.Equal(0m, metrics.ProfitFactor); // 0 kâr / 10 zarar
        Assert.Equal(-10m, metrics.AverageTradeReturnPercent);
        Assert.Equal(1, metrics.StopLossTrades);
    }

    [Fact]
    public void MixedTrades_MatchHandCalculation()
    {
        var metrics = Calculate(
            [
                Trade(100m, 110m),                                  // +10, %10
                Trade(200m, 205m, BotSignalReason.TakeProfit),      // +5,  %2,5
                Trade(100m, 95m, BotSignalReason.StopLoss),         // -5,  %-5
                Trade(100m, 100m)                                   //  0,  %0: ne kâr ne zarar
            ],
            100m, 110m);

        Assert.Equal(50m, metrics.WinRatePercent);                  // 2 kârlı / 4 işlem
        Assert.Equal(3m, metrics.ProfitFactor);                     // 15 / 5
        Assert.Equal(1.875m, metrics.AverageTradeReturnPercent);    // (10 + 2,5 - 5 + 0) / 4
        Assert.Equal(1, metrics.StopLossTrades);
        Assert.Equal(1, metrics.TakeProfitTrades);
    }

    // ── Al ve tut ───────────────────────────────────────────────

    [Fact]
    public void BuyAndHold_ComparedWithStrategyReturn()
    {
        var metrics = BacktestMetrics.Calculate([], Curve(200m, 220m), 100m, 125m);

        Assert.Equal(10m, metrics.StrategyReturnPercent);
        Assert.Equal(25m, metrics.BuyAndHoldReturnPercent);
        Assert.Equal(-15m, metrics.BuyAndHoldDifferencePercent);
    }

    [Fact]
    public void EmptyCurve_ReturnsAreNullWithoutDivideByZero()
    {
        var metrics = BacktestMetrics.Calculate([], Curve(), null, null);

        Assert.Null(metrics.StrategyReturnPercent);
        Assert.Null(metrics.BuyAndHoldReturnPercent);
        Assert.Null(metrics.BuyAndHoldDifferencePercent);
        Assert.Empty(metrics.EquityCurve);
    }

    // ── Eğriyi seyreltme ────────────────────────────────────────

    [Fact]
    public void Downsample_ShortCurve_IsUnchanged()
    {
        var curve = Curve(100m, 90m, 110m);

        Assert.Equal(curve, BacktestMetrics.Downsample(curve, 300));
    }

    [Theory]
    [InlineData(301, 17)]
    [InlineData(1440, 777)]
    [InlineData(10_000, 3)]
    public void Downsample_LongCurve_KeepsFirstLastAndLowest(int length, int lowestIndex)
    {
        var values = Enumerable.Range(0, length).Select(i => 1000m + i).ToArray();
        values[lowestIndex] = 1m;
        var curve = Curve(values);

        var sampled = BacktestMetrics.Downsample(curve, BacktestMetrics.MaxCurvePoints);

        Assert.True(sampled.Count <= BacktestMetrics.MaxCurvePoints);
        Assert.Equal(curve[0], sampled[0]);
        Assert.Equal(curve[^1], sampled[^1]);
        Assert.Contains(curve[lowestIndex], sampled);
        Assert.Equal(sampled.OrderBy(p => p.Timestamp), sampled);
    }

    [Fact]
    public void Calculate_DrawdownUsesFullCurve_EvenWhenPeakIsSampledOut()
    {
        // 2000 noktalık eğride tek noktalık tepe seyreltmede düşebilir, düşüş yine de tam eğriden hesaplanır.
        var values = Enumerable.Repeat(100m, 2000).ToArray();
        values[501] = 200m;
        values[1500] = 50m;

        var metrics = BacktestMetrics.Calculate([], Curve(values), 100m, 100m);

        Assert.Equal(75m, metrics.MaxDrawdownPercent); // 200'den 50'ye
        Assert.True(metrics.EquityCurve.Count <= BacktestMetrics.MaxCurvePoints);
    }

    // ── Simülasyonla birlikte ───────────────────────────────────

    private static TradingBot Bot(decimal? stopLossPercent = null) => new()
    {
        Symbol = "BTCUSDT",
        BuyRsiThreshold = 30m,
        SellRsiThreshold = 70m,
        TradeQuantity = 2m,
        StopLossPercent = stopLossPercent
    };

    private static List<BinanceKlineCandle> Candles(params decimal[] closes)
        => closes.Select((close, i) => new BinanceKlineCandle(Start.AddMinutes(i), close)).ToList();

    private static readonly decimal?[] Rsi = [20m, 40m, 45m, 80m];

    [Fact]
    public void Simulate_FillsMetricsAndEquityCurve()
    {
        // Sermaye: 2 × 100 = 200. Değerler: 200, 194, 188 (%6 düşüşte stop-loss, -12), 188.
        var result = BacktestService.Simulate(
            Bot(stopLossPercent: 5m), Candles(100m, 97m, 94m, 120m), Rsi, Start, Start.AddMinutes(3));

        var metrics = result.Metrics;
        Assert.Equal([200m, 194m, 188m, 188m], metrics.EquityCurve.Select(p => p.Value));
        Assert.Equal(6m, metrics.MaxDrawdownPercent);           // 12 / 200
        Assert.Equal(-6m, metrics.StrategyReturnPercent);       // 188'e göre
        Assert.Equal(20m, metrics.BuyAndHoldReturnPercent);     // 100'den 120'ye
        Assert.Equal(-26m, metrics.BuyAndHoldDifferencePercent);
        Assert.Equal(0m, metrics.WinRatePercent);
        Assert.Equal(1, metrics.StopLossTrades);
    }

    [Fact]
    public void Simulate_WithAndWithoutRiskLimits_ProduceDifferentMetrics()
    {
        var bot = Bot(stopLossPercent: 5m);
        var candles = Candles(100m, 97m, 94m, 120m);
        var end = Start.AddMinutes(3);

        var withLimits = BacktestService.Simulate(bot, candles, Rsi, Start, end).Metrics;
        var withoutLimits = BacktestService.Simulate(bot, candles, Rsi, Start, end, applyRiskLimits: false).Metrics;

        Assert.Equal(-6m, withLimits.StrategyReturnPercent);
        Assert.Equal(20m, withoutLimits.StrategyReturnPercent); // stop-loss yok, RSI 80'de 120'den satar
        Assert.Equal(100m, withoutLimits.WinRatePercent);
        Assert.Equal(0, withoutLimits.StopLossTrades);
    }

    [Fact]
    public void Simulate_OpenPositionAtEnd_CountsInEquityButNotInTrades()
    {
        // Pozisyon kapanmıyor: değer 200'den 240'a çıkıyor ama kapanan işlem yok.
        var result = BacktestService.Simulate(
            Bot(), Candles(100m, 120m), [20m, 40m], Start, Start.AddMinutes(1));

        Assert.Equal(20m, result.Metrics.StrategyReturnPercent);
        Assert.Null(result.Metrics.WinRatePercent);
        Assert.Equal(result.Summary.NetProfitLoss, result.Metrics.EquityCurve[^1].Value - result.Metrics.EquityCurve[0].Value);
    }
}
