using System.Diagnostics;
using System.Reflection;
using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CryptoTracker.API.Tests;

public class BacktestServiceTests
{
    private const int User1 = 1;
    private const int User2 = 2;

    [Fact]
    public async Task Run_InvalidDateRange_EqualDates_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db, CreateKlineMock().Object);

        var when = DateTime.UtcNow.AddDays(-2);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RunAsync(User1, 1, new BacktestRequestDto(when, when)));
    }

    [Fact]
    public async Task Run_InvalidDateRange_StartAfterEnd_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db, CreateKlineMock().Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RunAsync(User1, 1, new BacktestRequestDto(
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow.AddDays(-3))));
    }

    [Fact]
    public async Task Run_EndDateTooFarInFuture_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db, CreateKlineMock().Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RunAsync(User1, 1, new BacktestRequestDto(
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow.AddDays(5))));
    }

    [Fact]
    public void ValidateDateRange_UnspecifiedKind_Throws()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var end = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Unspecified);

        var ex = Assert.Throws<ArgumentException>(() =>
            BacktestService.ValidateDateRange(start, end));

        Assert.Contains("Unspecified", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateDateRange_UtcKind_Accepted()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        BacktestService.ValidateDateRange(start, end);
    }

    [Fact]
    public async Task Run_NonexistentBot_ThrowsNotFound()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var service = CreateService(db, CreateKlineMock().Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.RunAsync(User1, 999, new BacktestRequestDto(
                DateTime.UtcNow.AddDays(-2),
                DateTime.UtcNow.AddDays(-1))));
    }

    [Fact]
    public async Task Run_OtherUsersBot_ThrowsNotFound()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = await SeedBotAsync(db, User1);
        var service = CreateService(db, CreateKlineMock().Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.RunAsync(User2, bot.Id, new BacktestRequestDto(
                DateTime.UtcNow.AddDays(-2),
                DateTime.UtcNow.AddDays(-1))));
    }

    [Fact]
    public async Task Run_EmptyBinanceData_Throws()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = await SeedBotAsync(db, User1);

        var klineMock = CreateKlineMock(Array.Empty<BinanceKlineCandle>());
        var service = CreateService(db, klineMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunAsync(User1, bot.Id, new BacktestRequestDto(
                DateTime.UtcNow.AddDays(-2),
                DateTime.UtcNow.AddDays(-1))));
    }

    [Fact]
    public async Task Run_InsufficientCandleHistory_Throws()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = await SeedBotAsync(db, User1);

        var start = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 1, 12, 10, 0, DateTimeKind.Utc);

        var candles = Enumerable.Range(0, 5)
            .Select(i => new BinanceKlineCandle(start.AddMinutes(i), 100m + i))
            .ToList();

        var service = CreateService(db, CreateKlineMock(candles).Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunAsync(User1, bot.Id, new BacktestRequestDto(start, end)));

        Assert.Contains("yetersiz", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_NonPositiveTradeQuantity_Throws()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = await SeedBotAsync(db, User1);
        bot.TradeQuantity = 0m;
        await db.SaveChangesAsync();

        var start = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var candles = Enumerable.Range(0, 30)
            .Select(i => new BinanceKlineCandle(start.AddMinutes(i - 14), 100m + i))
            .ToList();

        var service = CreateService(db, CreateKlineMock(candles).Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RunAsync(User1, bot.Id, new BacktestRequestDto(start, start.AddMinutes(10))));

        Assert.Contains("trade quantity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Simulate_RsiBacktest_ProducesDeterministicSignals()
    {
        var bot = CreateBot();
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = new List<BinanceKlineCandle>();
        var rsi = new List<decimal?>();

        for (var i = 0; i < 14; i++)
        {
            candles.Add(new BinanceKlineCandle(start.AddMinutes(i - 14), 100m));
            rsi.Add(null);
        }

        candles.Add(new BinanceKlineCandle(start, 90m));
        rsi.Add(25m);
        candles.Add(new BinanceKlineCandle(start.AddMinutes(1), 95m));
        rsi.Add(50m);
        candles.Add(new BinanceKlineCandle(start.AddMinutes(2), 110m));
        rsi.Add(75m);

        var result = BacktestService.Simulate(bot, candles, rsi, start, start.AddMinutes(2));

        Assert.Equal(2, result.Summary.TotalSignals);
        Assert.Equal(1, result.Summary.BuySignals);
        Assert.Equal(1, result.Summary.SellSignals);

        Assert.Equal(start, result.Signals[0].Timestamp);
        Assert.Equal("BUY", result.Signals[0].Type);
        Assert.Equal(90m, result.Signals[0].Price);
        Assert.Equal(25m, result.Signals[0].Rsi);

        Assert.Equal(start.AddMinutes(2), result.Signals[1].Timestamp);
        Assert.Equal("SELL", result.Signals[1].Type);
        Assert.Equal(110m, result.Signals[1].Price);
        Assert.Equal(75m, result.Signals[1].Rsi);
    }

    [Fact]
    public void Simulate_BuyZoneHeld_EmitsOnlyOneBuy()
    {
        // CASE A: RSI stays <= buy threshold for consecutive bars.
        var bot = CreateBot();
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = new[]
        {
            new BinanceKlineCandle(start, 100m),
            new BinanceKlineCandle(start.AddMinutes(1), 99m),
            new BinanceKlineCandle(start.AddMinutes(2), 98m),
            new BinanceKlineCandle(start.AddMinutes(3), 97m),
            new BinanceKlineCandle(start.AddMinutes(4), 96m)
        };
        var rsi = new decimal?[] { 29m, 28m, 27m, 28m, 29m };

        var result = BacktestService.Simulate(bot, candles, rsi, start, start.AddMinutes(4));

        Assert.Equal(1, result.Summary.BuySignals);
        Assert.Equal(1, result.Summary.TotalSignals);
        Assert.Equal(start, result.Signals[0].Timestamp);
        Assert.Equal(100m, result.Signals[0].Price);
    }

    [Fact]
    public void Simulate_BuyZoneExitAndReentry_EmitsTwoBuys()
    {
        // CASE B: above → enter buy → stay → exit → enter again.
        var bot = CreateBot();
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = new[]
        {
            new BinanceKlineCandle(start, 110m),
            new BinanceKlineCandle(start.AddMinutes(1), 100m),
            new BinanceKlineCandle(start.AddMinutes(2), 99m),
            new BinanceKlineCandle(start.AddMinutes(3), 105m),
            new BinanceKlineCandle(start.AddMinutes(4), 95m)
        };
        var rsi = new decimal?[] { 40m, 25m, 20m, 45m, 22m };

        var result = BacktestService.Simulate(bot, candles, rsi, start, start.AddMinutes(4));

        Assert.Equal(2, result.Summary.BuySignals);
        Assert.Equal(start.AddMinutes(1), result.Signals[0].Timestamp);
        Assert.Equal(100m, result.Signals[0].Price);
        Assert.Equal(start.AddMinutes(4), result.Signals[1].Timestamp);
        Assert.Equal(95m, result.Signals[1].Price);
    }

    [Fact]
    public void Simulate_SellZoneHeld_EmitsOnlyOneSell()
    {
        // CASE C: SELL zone de-duplication.
        var bot = CreateBot();
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = new[]
        {
            new BinanceKlineCandle(start, 100m),
            new BinanceKlineCandle(start.AddMinutes(1), 110m),
            new BinanceKlineCandle(start.AddMinutes(2), 120m),
            new BinanceKlineCandle(start.AddMinutes(3), 125m)
        };
        var rsi = new decimal?[] { 80m, 85m, 90m, 88m };

        var result = BacktestService.Simulate(bot, candles, rsi, start, start.AddMinutes(3));

        Assert.Equal(1, result.Summary.SellSignals);
        Assert.Equal(1, result.Summary.TotalSignals);
        Assert.Equal(start, result.Signals[0].Timestamp);
        Assert.Equal(100m, result.Signals[0].Price);
    }

    [Fact]
    public void Simulate_SellZoneExitAndReentry_EmitsTwoSells()
    {
        var bot = CreateBot();
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = new[]
        {
            new BinanceKlineCandle(start, 100m),
            new BinanceKlineCandle(start.AddMinutes(1), 110m),
            new BinanceKlineCandle(start.AddMinutes(2), 105m),
            new BinanceKlineCandle(start.AddMinutes(3), 120m)
        };
        var rsi = new decimal?[] { 50m, 80m, 60m, 85m };

        var result = BacktestService.Simulate(bot, candles, rsi, start, start.AddMinutes(3));

        Assert.Equal(2, result.Summary.SellSignals);
        Assert.Equal(start.AddMinutes(1), result.Signals[0].Timestamp);
        Assert.Equal(110m, result.Signals[0].Price);
        Assert.Equal(start.AddMinutes(3), result.Signals[1].Timestamp);
        Assert.Equal(120m, result.Signals[1].Price);
    }

    [Fact]
    public void Simulate_FirstInRangeAlreadyInBuyZone_EmitsOneBuy()
    {
        // CASE D: first valid in-range RSI already in BUY zone.
        var bot = CreateBot();
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = new[]
        {
            new BinanceKlineCandle(start, 90m),
            new BinanceKlineCandle(start.AddMinutes(1), 91m)
        };
        var rsi = new decimal?[] { 18m, 19m };

        var result = BacktestService.Simulate(bot, candles, rsi, start, start.AddMinutes(1));

        Assert.Single(result.Signals);
        Assert.Equal("BUY", result.Signals[0].Type);
        Assert.Equal(start, result.Signals[0].Timestamp);
        Assert.Equal(90m, result.Signals[0].Price);
    }

    [Fact]
    public void Simulate_BuyThenSellZoneEntry_ExactTimestampsAndPrices()
    {
        // CASE E: BUY and SELL entered sequentially.
        var bot = CreateBot(quantity: 1m);
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = new[]
        {
            new BinanceKlineCandle(start, 100m),
            new BinanceKlineCandle(start.AddMinutes(1), 101m),
            new BinanceKlineCandle(start.AddMinutes(2), 130m)
        };
        var rsi = new decimal?[] { 40m, 22m, 80m };

        var result = BacktestService.Simulate(bot, candles, rsi, start, start.AddMinutes(2));

        Assert.Equal(2, result.Summary.TotalSignals);
        Assert.Equal("BUY", result.Signals[0].Type);
        Assert.Equal(start.AddMinutes(1), result.Signals[0].Timestamp);
        Assert.Equal(101m, result.Signals[0].Price);
        Assert.Equal(22m, result.Signals[0].Rsi);

        Assert.Equal("SELL", result.Signals[1].Type);
        Assert.Equal(start.AddMinutes(2), result.Signals[1].Timestamp);
        Assert.Equal(130m, result.Signals[1].Price);
        Assert.Equal(80m, result.Signals[1].Rsi);
        Assert.Equal(29m, result.Summary.RealizedProfitLoss);
    }

    [Fact]
    public void Simulate_BuyThenSell_PnLIsCorrect()
    {
        var bot = CreateBot(quantity: 2m);
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = new[]
        {
            new BinanceKlineCandle(start, 100m),
            new BinanceKlineCandle(start.AddMinutes(1), 120m)
        };
        var rsi = new decimal?[] { 20m, 80m };

        var result = BacktestService.Simulate(bot, candles, rsi, start, start.AddMinutes(1));

        Assert.Equal(1, result.Summary.CompletedTrades);
        Assert.Equal(1, result.Summary.WinningTrades);
        Assert.Equal(0, result.Summary.LosingTrades);
        Assert.Equal(40m, result.Summary.RealizedProfitLoss);
        Assert.Equal(0m, result.Summary.UnrealizedProfitLoss);
        Assert.Equal(40m, result.Summary.NetProfitLoss);
        Assert.Equal(20m, result.Summary.RealizedReturnPercentage);
    }

    [Fact]
    public void Simulate_DuplicateBuyWhileLong_DoesNotOpenSecondPosition()
    {
        // Zone-entry emits only one BUY while RSI stays in buy zone;
        // position machine also blocks duplicate entries if another BUY appears after exit/reentry.
        var bot = CreateBot();
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = new[]
        {
            new BinanceKlineCandle(start, 100m),
            new BinanceKlineCandle(start.AddMinutes(1), 90m),
            new BinanceKlineCandle(start.AddMinutes(2), 105m),
            new BinanceKlineCandle(start.AddMinutes(3), 85m),
            new BinanceKlineCandle(start.AddMinutes(4), 130m)
        };
        // BUY, stay, exit, re-enter BUY while still long, then SELL.
        var rsi = new decimal?[] { 20m, 15m, 40m, 18m, 80m };

        var result = BacktestService.Simulate(bot, candles, rsi, start, start.AddMinutes(4));

        Assert.Equal(2, result.Summary.BuySignals);
        Assert.Equal(1, result.Summary.SellSignals);
        Assert.Equal(1, result.Summary.CompletedTrades);
        // Entry at first BUY 100; second BUY ignored for position; SELL at 130 → PnL 30
        Assert.Equal(30m, result.Summary.RealizedProfitLoss);
    }

    [Fact]
    public void Simulate_SellWithNoPosition_DoesNotCreateInvalidPnL()
    {
        var bot = CreateBot();
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = new[]
        {
            new BinanceKlineCandle(start, 100m),
            new BinanceKlineCandle(start.AddMinutes(1), 110m)
        };
        var rsi = new decimal?[] { 80m, 85m };

        var result = BacktestService.Simulate(bot, candles, rsi, start, start.AddMinutes(1));

        Assert.Equal(1, result.Summary.SellSignals);
        Assert.Equal(0, result.Summary.CompletedTrades);
        Assert.Equal(0m, result.Summary.RealizedProfitLoss);
        Assert.Equal(0m, result.Summary.UnrealizedProfitLoss);
        Assert.Equal(0m, result.Summary.NetProfitLoss);
    }

    [Fact]
    public void Simulate_OpenPositionAtEnd_ReportsUnrealizedPnL()
    {
        var bot = CreateBot();
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = new[]
        {
            new BinanceKlineCandle(start, 100m),
            new BinanceKlineCandle(start.AddMinutes(1), 50m),
            new BinanceKlineCandle(start.AddMinutes(2), 115m)
        };
        var rsi = new decimal?[] { 20m, 50m, 55m };

        var result = BacktestService.Simulate(bot, candles, rsi, start, start.AddMinutes(2));

        Assert.Equal(0, result.Summary.CompletedTrades);
        Assert.Equal(0m, result.Summary.RealizedProfitLoss);
        Assert.Equal(15m, result.Summary.UnrealizedProfitLoss);
        Assert.Equal(15m, result.Summary.NetProfitLoss);
        // Realized return remains 0 while net includes unrealized.
        Assert.Equal(0m, result.Summary.RealizedReturnPercentage);
    }

    [Fact]
    public async Task Run_DoesNotPersistTradingState_AndHasNoTradingDependencies()
    {
        // BacktestService is constructed with AppDbContext + IBinanceKlineService only.
        // Architectural isolation (no IPortfolioService) is the primary safety boundary.
        var ctorParams = typeof(BacktestService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType)
            .ToArray();

        Assert.DoesNotContain(typeof(IPortfolioService), ctorParams);
        Assert.Equal(2, ctorParams.Length);
        Assert.Contains(typeof(AppDbContext), ctorParams);
        Assert.Contains(typeof(IBinanceKlineService), ctorParams);

        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = await SeedBotAsync(db, User1);

        var start = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(20);

        var candles = Enumerable.Range(0, 35)
            .Select(i => new BinanceKlineCandle(
                start.AddMinutes(i - 14),
                200m - i))
            .ToList();

        var service = new BacktestService(db, CreateKlineMock(candles).Object);

        var result = await service.RunAsync(
            User1,
            bot.Id,
            new BacktestRequestDto(start, end));

        Assert.NotNull(result);
        Assert.Equal("RSI", result.Strategy);
        Assert.True(result.Summary.BuySignals >= 1);

        Assert.Empty(await db.BotSignals.ToListAsync());
        Assert.Empty(await db.Transactions.ToListAsync());
        Assert.Empty(await db.PortfolioHoldings.ToListAsync());

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == User1);
        Assert.Equal(10_000m, user.VirtualBalance);

        var unchangedBot = await db.TradingBots.AsNoTracking().FirstAsync(b => b.Id == bot.Id);
        Assert.Equal(bot.IsActive, unchangedBot.IsActive);
        Assert.Equal(bot.TradeQuantity, unchangedBot.TradeQuantity);
    }

    [Fact]
    public async Task Run_WithKnownPriceSeries_SignalPricesMatchCandles()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = await SeedBotAsync(db, User1);

        var start = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(20);

        var candles = Enumerable.Range(0, 40)
            .Select(i => new BinanceKlineCandle(
                start.AddMinutes(i - 14),
                500m - (i * 5m)))
            .ToList();

        var service = CreateService(db, CreateKlineMock(candles).Object);
        var result = await service.RunAsync(User1, bot.Id, new BacktestRequestDto(start, end));

        Assert.NotEmpty(result.Signals);

        foreach (var signal in result.Signals)
        {
            var candle = candles.Single(c => c.OpenTimeUtc == signal.Timestamp);
            Assert.Equal(candle.ClosePrice, signal.Price);
            Assert.NotNull(signal.Rsi);
            Assert.True(signal.Timestamp >= start && signal.Timestamp <= end);
        }
    }

    [Fact]
    public void Simulate_LargeN_CompletesQuickly()
    {
        // NO-NETWORK synthetic smoke: 10k bars, O(n) RSI + zone-entry simulation.
        const int n = 10_000;
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = new List<BinanceKlineCandle>(n);
        for (var i = 0; i < n; i++)
        {
            // Mild oscillation so RSI produces occasional zone entries.
            var price = 100m + (decimal)Math.Sin(i / 20.0) * 10m + (i % 50) * 0.01m;
            candles.Add(new BinanceKlineCandle(start.AddMinutes(i), price));
        }

        var bot = CreateBot();
        var sw = Stopwatch.StartNew();
        var closes = candles.Select(c => c.ClosePrice).ToList();
        var rsi = RsiCalculator.CalculateSeries(closes, RsiSignalEvaluator.Period);
        var result = BacktestService.Simulate(bot, candles, rsi, start, start.AddMinutes(n - 1));
        sw.Stop();

        Assert.NotNull(result.Summary);
        Assert.True(result.Summary.TotalSignals >= 0);
        Assert.All(result.Signals, s => Assert.True(s.Timestamp >= start));
        // Generous threshold to catch accidental O(n²), not to flake CI.
        Assert.True(
            sw.Elapsed < TimeSpan.FromSeconds(3),
            $"Large-N backtest took {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    // ── Helpers ─────────────────────────────────────────────────

    private static TradingBot CreateBot(decimal quantity = 1m) => new()
    {
        Id = 1,
        Symbol = "BTCUSDT",
        BuyRsiThreshold = 30m,
        SellRsiThreshold = 70m,
        TradeQuantity = quantity
    };

    private static AppDbContext CreateDb(string? name = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedUsersAsync(AppDbContext db)
    {
        db.Users.AddRange(
            new User { Id = User1, Username = "user1", Email = "u1@test.com", PasswordHash = "h" },
            new User { Id = User2, Username = "user2", Email = "u2@test.com", PasswordHash = "h" });
        await db.SaveChangesAsync();
    }

    private static async Task<TradingBot> SeedBotAsync(
        AppDbContext db,
        int userId,
        decimal buyThreshold = 30m,
        decimal sellThreshold = 70m)
    {
        var bot = new TradingBot
        {
            UserId = userId,
            Symbol = "BTCUSDT",
            TradeQuantity = 1m,
            BuyRsiThreshold = buyThreshold,
            SellRsiThreshold = sellThreshold,
            IsActive = true
        };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();
        return bot;
    }

    private static Mock<IBinanceKlineService> CreateKlineMock(
        IReadOnlyList<BinanceKlineCandle>? candles = null)
    {
        var mock = new Mock<IBinanceKlineService>();
        mock.Setup(s => s.GetHistoricalKlinesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candles ?? Array.Empty<BinanceKlineCandle>());
        return mock;
    }

    private static BacktestService CreateService(
        AppDbContext db,
        IBinanceKlineService klineService) =>
        new(db, klineService);
}
