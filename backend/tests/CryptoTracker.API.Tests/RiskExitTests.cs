using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CryptoTracker.API.Tests;

/// <summary>
/// Stop-loss / take-profit: saf karar fonksiyonu, backtest simülasyonu ve bot oluşturma doğrulaması.
/// Canlı bot (BotMonitorService) testleri BotMonitorServiceTests içinde.
/// </summary>
public class RiskExitTests
{
    private static readonly DateTime Start = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    // ── RiskExitEvaluator.Evaluate ──────────────────────────────

    [Theory]
    [InlineData(96, null)]   // %4 düşüş: %5 stop-loss'a ulaşmadı
    [InlineData(95, 1)]      // tam %5 düşüş: sınır dahil, satılır
    [InlineData(80, 1)]      // %20 düşüş
    [InlineData(109, null)]  // %9 yükseliş: %10 take-profit'e ulaşmadı
    [InlineData(110, 2)]     // tam %10 yükseliş: sınır dahil, satılır
    [InlineData(150, 2)]     // %50 yükseliş
    [InlineData(100, null)]  // fiyat değişmedi
    public void Evaluate_ReturnsExpectedReason(decimal currentPrice, int? expected)
    {
        var reason = RiskExitEvaluator.Evaluate(100m, currentPrice, 5m, 10m);

        Assert.Equal(expected is null ? null : (BotSignalReason)expected.Value, reason);
    }

    [Fact]
    public void Evaluate_NoLimits_NeverExits()
    {
        Assert.Null(RiskExitEvaluator.Evaluate(100m, 1m, null, null));
        Assert.Null(RiskExitEvaluator.Evaluate(100m, 1000m, null, null));
    }

    [Fact]
    public void Evaluate_OnlyStopLoss_IgnoresBigRise()
    {
        Assert.Null(RiskExitEvaluator.Evaluate(100m, 500m, 5m, null));
        Assert.Equal(BotSignalReason.StopLoss, RiskExitEvaluator.Evaluate(100m, 94m, 5m, null));
    }

    [Fact]
    public void Evaluate_OnlyTakeProfit_IgnoresBigDrop()
    {
        Assert.Null(RiskExitEvaluator.Evaluate(100m, 10m, null, 10m));
        Assert.Equal(BotSignalReason.TakeProfit, RiskExitEvaluator.Evaluate(100m, 111m, null, 10m));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(100, 0)]
    [InlineData(-5, 50)]
    public void Evaluate_InvalidPrices_ReturnsNull(decimal entryPrice, decimal currentPrice)
    {
        Assert.Null(RiskExitEvaluator.Evaluate(entryPrice, currentPrice, 5m, 10m));
    }

    // ── RiskExitEvaluator.FindOpenPositionEntryPrice ────────────

    [Fact]
    public void FindEntryPrice_LastApprovedIsBuy_ReturnsItsPrice()
    {
        var signals = new[]
        {
            Signal(1, BotSignalType.Buy, 80m, BotSignalStatus.Approved, minutesAgo: 30),
            Signal(2, BotSignalType.Sell, 90m, BotSignalStatus.Approved, minutesAgo: 20),
            Signal(3, BotSignalType.Buy, 100m, BotSignalStatus.Approved, minutesAgo: 10),
            Signal(4, BotSignalType.Sell, 105m, BotSignalStatus.Failed, minutesAgo: 5)
        };

        Assert.Equal(100m, RiskExitEvaluator.FindOpenPositionEntryPrice(signals));
    }

    [Fact]
    public void FindEntryPrice_LastApprovedIsSell_ReturnsNull()
    {
        var signals = new[]
        {
            Signal(1, BotSignalType.Buy, 100m, BotSignalStatus.Approved, minutesAgo: 20),
            Signal(2, BotSignalType.Sell, 110m, BotSignalStatus.Approved, minutesAgo: 10)
        };

        Assert.Null(RiskExitEvaluator.FindOpenPositionEntryPrice(signals));
    }

    [Fact]
    public void FindEntryPrice_NoApprovedSignal_ReturnsNull()
    {
        var signals = new[]
        {
            Signal(1, BotSignalType.Buy, 100m, BotSignalStatus.Failed, minutesAgo: 10)
        };

        Assert.Null(RiskExitEvaluator.FindOpenPositionEntryPrice(signals));
        Assert.Null(RiskExitEvaluator.FindOpenPositionEntryPrice(Array.Empty<BotSignal>()));
    }

    // ── Backtest ────────────────────────────────────────────────

    [Fact]
    public void Backtest_StopLoss_SellsAtLossAndMarksReason()
    {
        var bot = CreateBot(stopLossPercent: 5m);
        var candles = Candles(100m, 97m, 94m, 120m);
        var rsi = new decimal?[] { 20m, 40m, 45m, 80m };

        var result = BacktestService.Simulate(bot, candles, rsi, Start, Start.AddMinutes(3));

        // AL (strateji) → %6 düşüşte stop-loss SAT → RSI 80'de strateji SAT (pozisyon yok, PnL etkisiz)
        Assert.Equal(3, result.Signals.Count);
        Assert.Equal("BUY", result.Signals[0].Type);
        Assert.Equal("Strategy", result.Signals[0].Reason);

        Assert.Equal("SELL", result.Signals[1].Type);
        Assert.Equal("StopLoss", result.Signals[1].Reason);
        Assert.Equal(94m, result.Signals[1].Price);
        Assert.Equal(Start.AddMinutes(2), result.Signals[1].Timestamp);

        Assert.Equal("Strategy", result.Signals[2].Reason);

        Assert.Equal(1, result.Summary.CompletedTrades);
        Assert.Equal(1, result.Summary.LosingTrades);
        Assert.Equal(-6m, result.Summary.RealizedProfitLoss);
        Assert.Equal(0m, result.Summary.UnrealizedProfitLoss);
    }

    [Fact]
    public void Backtest_TakeProfit_SellsAtProfitAndMarksReason()
    {
        var bot = CreateBot(takeProfitPercent: 10m, quantity: 2m);
        var candles = Candles(100m, 105m, 111m);
        var rsi = new decimal?[] { 20m, 50m, 55m };

        var result = BacktestService.Simulate(bot, candles, rsi, Start, Start.AddMinutes(2));

        Assert.Equal(2, result.Signals.Count);
        Assert.Equal("TakeProfit", result.Signals[1].Reason);
        Assert.Equal(111m, result.Signals[1].Price);
        Assert.Equal(1, result.Summary.WinningTrades);
        Assert.Equal(22m, result.Summary.RealizedProfitLoss);
    }

    [Fact]
    public void Backtest_RiskExitCandle_SkipsStrategySignalOnSameCandle()
    {
        var bot = CreateBot(stopLossPercent: 5m);
        var result = BacktestService.Simulate(
            bot, Candles(100m, 90m, 90m), CandleRsi(20m, 40m, 20m), Start, Start.AddMinutes(2));

        // Mum 1: stop-loss SAT. Mum 2: RSI 40 → 20 yeniden AL bölgesine giriş (strateji AL).
        Assert.Equal(new[] { "BUY", "SELL", "BUY" }, result.Signals.Select(s => s.Type));
        Assert.Equal(new[] { "Strategy", "StopLoss", "Strategy" }, result.Signals.Select(s => s.Reason));

        // Aynı mumda risk satışı ve strateji sinyali çakışırsa yalnızca risk satışı yazılır.
        var sameCandle = BacktestService.Simulate(
            bot, Candles(100m, 90m), CandleRsi(20m, 80m), Start, Start.AddMinutes(1));

        Assert.Equal(2, sameCandle.Signals.Count);
        Assert.Equal("StopLoss", sameCandle.Signals[1].Reason);
    }

    [Fact]
    public void Backtest_NoLimits_BehavesLikeBefore()
    {
        var bot = CreateBot();
        var result = BacktestService.Simulate(
            bot, Candles(100m, 50m, 115m), CandleRsi(20m, 40m, 80m), Start, Start.AddMinutes(2));

        // %50 düşüşe rağmen satış yok; sadece strateji AL ve SAT.
        Assert.Equal(2, result.Signals.Count);
        Assert.All(result.Signals, s => Assert.Equal("Strategy", s.Reason));
        Assert.Equal(15m, result.Summary.RealizedProfitLoss);
    }

    [Fact]
    public void Backtest_EmaBot_AlsoAppliesStopLoss()
    {
        // EMA botu da aynı BuildResult'ı kullanır: önce düşüş, sonra sert yükseliş (AL), sonra çöküş.
        var bot = CreateBot(stopLossPercent: 5m);
        bot.Strategy = BotStrategy.EmaCrossover;
        bot.ShortEmaPeriod = 2;
        bot.LongEmaPeriod = 4;

        var prices = new List<decimal>();
        for (var i = 0; i < 20; i++) prices.Add(100m - i);     // düşüş: kısa EMA uzunun altında
        prices.AddRange(new[] { 95m, 110m, 120m });            // yükseliş: altın kesişim (AL)
        prices.AddRange(new[] { 120m, 85m });                  // girişe (95) göre ~%10 düşüş: stop-loss

        var candles = prices
            .Select((p, i) => new BinanceKlineCandle(Start.AddMinutes(i), p))
            .ToList();

        var result = BacktestService.SimulateEma(bot, candles, Start, Start.AddMinutes(prices.Count - 1));

        Assert.Contains(result.Signals, s => s.Type == "BUY" && s.Reason == "Strategy");
        var stop = Assert.Single(result.Signals, s => s.Reason == "StopLoss");
        Assert.Equal("SELL", stop.Type);
        Assert.Equal(85m, stop.Price);
        // Aynı mumdaki ölüm kesişimi (strateji SAT) yazılmaz; risk satışı önce gelir.
        Assert.Equal("StopLoss", result.Signals[^1].Reason);
    }

    // ── Bot oluşturma doğrulaması ───────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(150)]
    public async Task CreateBot_InvalidStopLoss_Throws(decimal stopLoss)
    {
        await using var db = CreateDb();
        var service = CreateBotService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateBotAsync(1, new CreateBotRequest(
                "BTCUSDT", 30m, 70m, 1m, StopLossPercent: stopLoss)));

        Assert.Contains("Stop-loss", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CreateBot_InvalidTakeProfit_Throws(decimal takeProfit)
    {
        await using var db = CreateDb();
        var service = CreateBotService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateBotAsync(1, new CreateBotRequest(
                "BTCUSDT", 30m, 70m, 1m, TakeProfitPercent: takeProfit)));

        Assert.Contains("Take-profit", ex.Message);
    }

    [Fact]
    public async Task CreateBot_ValidLimits_AreSavedAndReturned()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Id = 1, Username = "user1", Email = "u1@test.com", PasswordHash = "h" });
        await db.SaveChangesAsync();
        var service = CreateBotService(db);

        var response = await service.CreateBotAsync(1, new CreateBotRequest(
            "BTCUSDT", 30m, 70m, 1m, StopLossPercent: 5m, TakeProfitPercent: 150m));

        Assert.Equal(5m, response.StopLossPercent);
        Assert.Equal(150m, response.TakeProfitPercent);

        var saved = await db.TradingBots.SingleAsync();
        Assert.Equal(5m, saved.StopLossPercent);
        Assert.Equal(150m, saved.TakeProfitPercent);
    }

    [Fact]
    public async Task CreateBot_WithoutLimits_LeavesThemEmpty()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Id = 1, Username = "user1", Email = "u1@test.com", PasswordHash = "h" });
        await db.SaveChangesAsync();
        var service = CreateBotService(db);

        var response = await service.CreateBotAsync(1, new CreateBotRequest("BTCUSDT", 30m, 70m, 1m));

        Assert.Null(response.StopLossPercent);
        Assert.Null(response.TakeProfitPercent);
    }

    // ── Yardımcılar ─────────────────────────────────────────────

    private static BotSignal Signal(
        int id, BotSignalType type, decimal price, BotSignalStatus status, int minutesAgo) => new()
    {
        Id = id,
        BotId = 1,
        SignalType = type,
        PriceAtSignal = price,
        Status = status,
        CreatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo)
    };

    private static TradingBot CreateBot(
        decimal? stopLossPercent = null,
        decimal? takeProfitPercent = null,
        decimal quantity = 1m) => new()
    {
        Id = 1,
        Symbol = "BTCUSDT",
        BuyRsiThreshold = 30m,
        SellRsiThreshold = 70m,
        TradeQuantity = quantity,
        StopLossPercent = stopLossPercent,
        TakeProfitPercent = takeProfitPercent
    };

    private static List<BinanceKlineCandle> Candles(params decimal[] closes) =>
        closes.Select((close, i) => new BinanceKlineCandle(Start.AddMinutes(i), close)).ToList();

    private static decimal?[] CandleRsi(params decimal[] values) =>
        values.Select(v => (decimal?)v).ToArray();

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static BotService CreateBotService(AppDbContext db) =>
        new(db, new PortfolioService(db),
            Options.Create(new TradingBotOptions { SignalExpirationMinutes = 15 }));
}
