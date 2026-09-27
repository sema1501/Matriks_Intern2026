using CryptoTracker.API.Models;
using CryptoTracker.API.Services;

namespace CryptoTracker.API.Tests;

/// <summary>
/// #81 — EMA kesişim stratejisinin backtest tarafı.
///
/// Örnek veri (kısa EMA=2, uzun EMA=3) Python ile ayrıca hesaplanıp çapraz kontrol edildi:
///
///  i | fiyat | EMA2      | EMA3     | fark (EMA2-EMA3)
///  3 |  10   | 10.0000   | 10.0000  |  0.0000
///  4 |  13   | 12.0000   | 11.5000  | +0.5000   ← yukarı kesişim → BUY
///  5 |  16   | 14.6667   | 13.7500  | +0.9167
///  6 |  12   | 12.8889   | 12.8750  | +0.0139
///  7 |   8   |  9.6296   | 10.4375  | -0.8079   ← aşağı kesişim → SELL
///  8 |   8   |  8.5432   |  9.2188  | -0.6755
/// </summary>
public class EmaBacktestTests
{
    private static readonly DateTime T0 = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly decimal[] Prices = [10m, 10m, 10m, 10m, 13m, 16m, 12m, 8m, 8m];

    private static List<BinanceKlineCandle> Candles() =>
        Prices.Select((p, i) => new BinanceKlineCandle(T0.AddMinutes(i), p)).ToList();

    private static TradingBot EmaBot(decimal quantity = 1m) => new()
    {
        Id = 1,
        Symbol = "BTCUSDT",
        Strategy = BotStrategy.EmaCrossover,
        ShortEmaPeriod = 2,
        LongEmaPeriod = 3,
        TradeQuantity = quantity
    };

    [Fact]
    public void SimulateEma_ProducesBuyOnGoldenCross_AndSellOnDeathCross()
    {
        var result = BacktestService.SimulateEma(
            EmaBot(), Candles(), T0.AddMinutes(3), T0.AddMinutes(8));

        Assert.Equal(2, result.Summary.TotalSignals);

        Assert.Equal("BUY", result.Signals[0].Type);
        Assert.Equal(T0.AddMinutes(4), result.Signals[0].Timestamp);
        Assert.Equal(13m, result.Signals[0].Price);
        Assert.Null(result.Signals[0].Rsi); // EMA sinyalinde RSI yok

        Assert.Equal("SELL", result.Signals[1].Type);
        Assert.Equal(T0.AddMinutes(7), result.Signals[1].Timestamp);
        Assert.Equal(8m, result.Signals[1].Price);
    }

    [Fact]
    public void SimulateEma_ComputesPnLFromCrossoverTrades()
    {
        var result = BacktestService.SimulateEma(
            EmaBot(quantity: 2m), Candles(), T0.AddMinutes(3), T0.AddMinutes(8));

        // 13'ten al, 8'den sat, miktar 2 → (8 - 13) * 2 = -10
        Assert.Equal(1, result.Summary.CompletedTrades);
        Assert.Equal(0, result.Summary.WinningTrades);
        Assert.Equal(1, result.Summary.LosingTrades);
        Assert.Equal(-10m, result.Summary.RealizedProfitLoss);
        Assert.Equal(0m, result.Summary.UnrealizedProfitLoss);
    }

    [Fact]
    public void SimulateEma_FirstInRangeCandle_OnlySeedsPrevious()
    {
        // Aralık 4. mumdan başlarsa, 4. mum yalnızca "önceki" değer olur;
        // ısınma verisiyle yapılan kesişim sinyal üretmemeli (RSI tarafıyla aynı kural).
        var result = BacktestService.SimulateEma(
            EmaBot(), Candles(), T0.AddMinutes(4), T0.AddMinutes(8));

        Assert.Single(result.Signals);
        Assert.Equal("SELL", result.Signals[0].Type);
        Assert.Equal(0, result.Summary.CompletedTrades); // pozisyon yokken SELL → PnL etkisi yok
    }

    [Fact]
    public void SimulateEma_WithoutPeriods_Throws()
    {
        var bot = EmaBot();
        bot.LongEmaPeriod = null;

        Assert.Throws<ArgumentException>(() =>
            BacktestService.SimulateEma(bot, Candles(), T0, T0.AddMinutes(8)));
    }

    [Theory]
    [InlineData(BotStrategy.RsiThreshold, "RSI")]
    [InlineData(BotStrategy.EmaCrossover, "EMA")]
    public void StrategyLabel_ReflectsBotStrategy(BotStrategy strategy, string expected)
    {
        Assert.Equal(expected, BacktestService.StrategyLabel(strategy));
    }
}
