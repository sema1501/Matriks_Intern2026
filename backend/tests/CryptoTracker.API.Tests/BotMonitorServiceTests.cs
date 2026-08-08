using System.Reflection;
using CryptoTracker.API.Data;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CryptoTracker.API.Tests;

public class BotMonitorServiceTests
{
    [Fact]
    public async Task ZoneEntryBuy_AutoExecutesPortfolioAndApprovesSignal()
    {
        await using var sp = CreateServiceProvider(seedUserBalance: 10_000m);
        var botId = await SeedBotAsync(sp, userId: 1, quantity: 1m);

        // Previous bar RSI above buy; last bar deep in buy zone via falling prices.
        // Build closes so CalculateSeries yields previous > 30 and current <= 30.
        var closes = BuildZoneEntryBuyCloses();
        var klineMock = CreateKlineMock(closes);
        var monitor = CreateMonitorService(sp, klineMock.Object);

        await EvaluateBotsAsync(monitor);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var signal = await db.BotSignals.SingleAsync();
        Assert.Equal(botId, signal.BotId);
        Assert.Equal(BotSignalType.Buy, signal.SignalType);
        Assert.Equal(BotSignalStatus.Approved, signal.Status);
        Assert.Equal(closes[^1], signal.PriceAtSignal);

        var user = await db.Users.SingleAsync(u => u.Id == 1);
        Assert.Equal(10_000m - closes[^1], user.VirtualBalance);

        var holding = await db.PortfolioHoldings.SingleAsync();
        Assert.Equal("BTCUSDT", holding.Symbol);
        Assert.Equal(1m, holding.Quantity);

        Assert.Single(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task ZoneEntrySell_AutoExecutesPortfolioAndApprovesSignal()
    {
        await using var sp = CreateServiceProvider(seedUserBalance: 10_000m);
        await SeedHoldingAsync(sp, userId: 1, symbol: "BTCUSDT", qty: 5m, avg: 50m);
        var botId = await SeedBotAsync(sp, userId: 1, quantity: 1m);

        var closes = BuildZoneEntrySellCloses();
        var monitor = CreateMonitorService(sp, CreateKlineMock(closes).Object);

        await EvaluateBotsAsync(monitor);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var signal = await db.BotSignals.SingleAsync();
        Assert.Equal(botId, signal.BotId);
        Assert.Equal(BotSignalType.Sell, signal.SignalType);
        Assert.Equal(BotSignalStatus.Approved, signal.Status);

        var user = await db.Users.SingleAsync(u => u.Id == 1);
        Assert.Equal(10_000m + closes[^1], user.VirtualBalance);

        var holding = await db.PortfolioHoldings.SingleAsync();
        Assert.Equal(4m, holding.Quantity);
        Assert.Single(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Buy_InsufficientBalance_MarksFailed_NoPortfolioChange()
    {
        await using var sp = CreateServiceProvider(seedUserBalance: 1m);
        await SeedBotAsync(sp, userId: 1, quantity: 1m);

        var closes = BuildZoneEntryBuyCloses();
        var monitor = CreateMonitorService(sp, CreateKlineMock(closes).Object);

        await EvaluateBotsAsync(monitor);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var signal = await db.BotSignals.SingleAsync();
        Assert.Equal(BotSignalStatus.Failed, signal.Status);
        Assert.Equal(BotSignalType.Buy, signal.SignalType);

        var user = await db.Users.SingleAsync(u => u.Id == 1);
        Assert.Equal(1m, user.VirtualBalance);
        Assert.Empty(await db.PortfolioHoldings.ToListAsync());
        Assert.Empty(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Sell_InsufficientHolding_MarksFailed_NoPortfolioChange()
    {
        await using var sp = CreateServiceProvider(seedUserBalance: 10_000m);
        await SeedBotAsync(sp, userId: 1, quantity: 1m);
        // no holdings

        var closes = BuildZoneEntrySellCloses();
        var monitor = CreateMonitorService(sp, CreateKlineMock(closes).Object);

        await EvaluateBotsAsync(monitor);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var signal = await db.BotSignals.SingleAsync();
        Assert.Equal(BotSignalStatus.Failed, signal.Status);

        var user = await db.Users.SingleAsync(u => u.Id == 1);
        Assert.Equal(10_000m, user.VirtualBalance);
        Assert.Empty(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task SuccessfulAutoBuy_LeavesBotActive()
    {
        await using var sp = CreateServiceProvider(seedUserBalance: 10_000m);
        var botId = await SeedBotAsync(sp, userId: 1, quantity: 1m);

        var closes = BuildZoneEntryBuyCloses();
        var monitor = CreateMonitorService(sp, CreateKlineMock(closes).Object);
        await EvaluateBotsAsync(monitor);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var signal = await db.BotSignals.AsNoTracking().SingleAsync();
        Assert.Equal(BotSignalStatus.Approved, signal.Status);
        Assert.Equal(BotSignalType.Buy, signal.SignalType);
        Assert.Single(await db.Transactions.AsNoTracking().ToListAsync());

        // Reload from DB — monitor loads bots AsNoTracking and must not mutate IsActive.
        var bot = await db.TradingBots.AsNoTracking().SingleAsync(b => b.Id == botId);
        Assert.True(bot.IsActive);
    }

    [Fact]
    public async Task SuccessfulAutoSell_LeavesBotActive()
    {
        await using var sp = CreateServiceProvider(seedUserBalance: 10_000m);
        await SeedHoldingAsync(sp, userId: 1, symbol: "BTCUSDT", qty: 5m, avg: 50m);
        var botId = await SeedBotAsync(sp, userId: 1, quantity: 1m);

        var closes = BuildZoneEntrySellCloses();
        var monitor = CreateMonitorService(sp, CreateKlineMock(closes).Object);
        await EvaluateBotsAsync(monitor);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var signal = await db.BotSignals.AsNoTracking().SingleAsync();
        Assert.Equal(BotSignalStatus.Approved, signal.Status);
        Assert.Equal(BotSignalType.Sell, signal.SignalType);
        Assert.Single(await db.Transactions.AsNoTracking().ToListAsync());

        var bot = await db.TradingBots.AsNoTracking().SingleAsync(b => b.Id == botId);
        Assert.True(bot.IsActive);
    }

    [Fact]
    public async Task FailedAutoExecution_LeavesBotActive()
    {
        await using var sp = CreateServiceProvider(seedUserBalance: 1m);
        var botId = await SeedBotAsync(sp, userId: 1, quantity: 1m);

        var closes = BuildZoneEntryBuyCloses();
        var monitor = CreateMonitorService(sp, CreateKlineMock(closes).Object);
        await EvaluateBotsAsync(monitor);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var signal = await db.BotSignals.AsNoTracking().SingleAsync();
        Assert.Equal(BotSignalStatus.Failed, signal.Status);
        Assert.Empty(await db.Transactions.AsNoTracking().ToListAsync());

        var bot = await db.TradingBots.AsNoTracking().SingleAsync(b => b.Id == botId);
        Assert.True(bot.IsActive);
    }

    [Fact]
    public async Task RepeatedBuyZoneEvaluations_ExecuteOnlyOnce()
    {
        await using var sp = CreateServiceProvider(seedUserBalance: 10_000m);
        await SeedBotAsync(sp, userId: 1, quantity: 1m);

        var closes = BuildZoneEntryBuyCloses();
        var klineMock = CreateKlineMock(closes);
        var monitor = CreateMonitorService(sp, klineMock.Object);

        await EvaluateBotsAsync(monitor);
        // Still in buy zone: previous and current both oversold → no new zone entry.
        await EvaluateBotsAsync(monitor);
        await EvaluateBotsAsync(monitor);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(1, await db.BotSignals.CountAsync());
        Assert.Equal(1, await db.Transactions.CountAsync());
        Assert.Equal(BotSignalStatus.Approved, (await db.BotSignals.SingleAsync()).Status);
    }

    [Fact]
    public async Task FormingCandleAfterClosedZoneEntry_DoesNotHideSignal_WhenClosedSeriesProvided()
    {
        // Production bug: Binance's open candle after a closed-bar zone entry leaves
        // both current+previous RSI in-zone → DetermineZoneEntrySignal returns null.
        // GetClosingPricesAsync now strips the forming candle so the monitor receives
        // the closed series (entry on last element) — proven here end-to-end.
        var entry = BuildZoneEntryBuyCloses();
        var withFormingStillInZone = entry.ToList();
        withFormingStillInZone.Add(entry[^1] - 5m);

        var shadowed = RsiCalculator.CalculateSeries(withFormingStillInZone, RsiSignalEvaluator.Period);
        Assert.Null(RsiSignalEvaluator.DetermineZoneEntrySignal(
            shadowed[^1]!.Value,
            shadowed[^2],
            30m,
            70m));

        await using var sp = CreateServiceProvider(seedUserBalance: 10_000m);
        await SeedBotAsync(sp, userId: 1, quantity: 1m);
        var monitor = CreateMonitorService(sp, CreateKlineMock(entry).Object);
        await EvaluateBotsAsync(monitor);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(BotSignalStatus.Approved, (await db.BotSignals.SingleAsync()).Status);
        Assert.Equal(BotSignalType.Buy, (await db.BotSignals.SingleAsync()).SignalType);
    }

    [Fact]
    public async Task ExitAndReenterBuyZone_AllowsSecondTrade()
    {
        await using var sp = CreateServiceProvider(seedUserBalance: 100_000m);
        await SeedBotAsync(sp, userId: 1, quantity: 1m);

        var entry = BuildZoneEntryBuyCloses(basePrice: 200m);
        var monitor1 = CreateMonitorService(sp, CreateKlineMock(entry).Object);
        await EvaluateBotsAsync(monitor1);

        var mid = BuildNeutralClosesFrom(entry);
        var monitor2 = CreateMonitorService(sp, CreateKlineMock(mid).Object);
        await EvaluateBotsAsync(monitor2);

        // Distinct price level so same-bar guard does not treat this as the first entry bar.
        var reentry = BuildZoneEntryBuyCloses(basePrice: 500m);
        var monitor3 = CreateMonitorService(sp, CreateKlineMock(reentry).Object);
        await EvaluateBotsAsync(monitor3);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var buys = await db.BotSignals
            .Where(s => s.SignalType == BotSignalType.Buy && s.Status == BotSignalStatus.Approved)
            .ToListAsync();
        Assert.Equal(2, buys.Count);
        Assert.Equal(2, await db.Transactions.CountAsync(t => t.Type == TransactionType.Buy));
    }

    [Fact]
    public async Task MultipleBotsWithSameSymbol_RequestKlinesOnlyOnce()
    {
        await using var sp = CreateServiceProvider(seedUserBalance: 100_000m);

        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User { Id = 101, Username = "a", Email = "a@t.com", PasswordHash = "h", VirtualBalance = 100_000m });
            db.Users.Add(new User { Id = 102, Username = "b", Email = "b@t.com", PasswordHash = "h", VirtualBalance = 100_000m });
            await db.SaveChangesAsync();

            db.TradingBots.AddRange(
                new TradingBot
                {
                    UserId = 101, Symbol = "BTCUSDT", IsActive = true,
                    BuyRsiThreshold = 30m, SellRsiThreshold = 70m, TradeQuantity = 0.01m
                },
                new TradingBot
                {
                    UserId = 102, Symbol = " btcusdt ", IsActive = true,
                    BuyRsiThreshold = 30m, SellRsiThreshold = 70m, TradeQuantity = 0.02m
                });
            await db.SaveChangesAsync();
        }

        var closes = BuildZoneEntryBuyCloses();
        var klineMock = CreateKlineMock(closes);
        var monitor = CreateMonitorService(sp, klineMock.Object);

        await EvaluateBotsAsync(monitor);

        klineMock.Verify(
            s => s.GetClosingPricesAsync(
                "BTCUSDT",
                "1m",
                100,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InactiveBot_IsNotEvaluated()
    {
        await using var sp = CreateServiceProvider(seedUserBalance: 10_000m);

        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User { Id = 401, Username = "x", Email = "x@t.com", PasswordHash = "h", VirtualBalance = 10_000m });
            await db.SaveChangesAsync();
            db.TradingBots.Add(new TradingBot
            {
                UserId = 401, Symbol = "SOLUSDT", IsActive = false,
                BuyRsiThreshold = 30m, SellRsiThreshold = 70m, TradeQuantity = 1m
            });
            await db.SaveChangesAsync();
        }

        var klineMock = CreateKlineMock(BuildZoneEntryBuyCloses());
        var monitor = CreateMonitorService(sp, klineMock.Object);

        await EvaluateBotsAsync(monitor);

        klineMock.Verify(
            s => s.GetClosingPricesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        await using var assertionScope = sp.CreateAsyncScope();
        var assertionDb = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await assertionDb.BotSignals.ToListAsync());
    }

    [Fact]
    public async Task Approve_AlreadyApprovedAutoSignal_DoesNotDoubleTrade()
    {
        await using var sp = CreateServiceProvider(seedUserBalance: 10_000m);
        var botId = await SeedBotAsync(sp, userId: 1, quantity: 1m);

        var closes = BuildZoneEntryBuyCloses();
        var monitor = CreateMonitorService(sp, CreateKlineMock(closes).Object);
        await EvaluateBotsAsync(monitor);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var signal = await db.BotSignals.SingleAsync();
        Assert.Equal(BotSignalStatus.Approved, signal.Status);

        var opts = Options.Create(new TradingBotOptions { SignalExpirationMinutes = 15 });
        var botService = new BotService(db, new PortfolioService(db), opts);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            botService.ApproveSignalAsync(1, signal.Id));

        Assert.Equal(1, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task UsesBotOwnerUserId_NotHttpContext()
    {
        await using var sp = CreateServiceProvider(seedUserBalance: 0m);
        // Owner user 7 has balance; ensure trade hits user 7.
        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User
            {
                Id = 7, Username = "owner", Email = "o@t.com", PasswordHash = "h",
                VirtualBalance = 50_000m
            });
            await db.SaveChangesAsync();
            db.TradingBots.Add(new TradingBot
            {
                UserId = 7, Symbol = "ETHUSDT", IsActive = true,
                BuyRsiThreshold = 30m, SellRsiThreshold = 70m, TradeQuantity = 1m
            });
            await db.SaveChangesAsync();
        }

        var closes = BuildZoneEntryBuyCloses();
        var monitor = CreateMonitorService(sp, CreateKlineMock(closes).Object);
        await EvaluateBotsAsync(monitor);

        await using var assertScope = sp.CreateAsyncScope();
        var db2 = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tx = await db2.Transactions.SingleAsync();
        Assert.Equal(7, tx.UserId);
        Assert.Equal(BotSignalStatus.Approved, (await db2.BotSignals.SingleAsync()).Status);
    }

    // ── Price series helpers ────────────────────────────────────

    /// <summary>
    /// Builds closes where previous RSI &gt; 30 and current RSI ≤ 30 (buy zone entry).
    /// </summary>
    private static List<decimal> BuildZoneEntryBuyCloses(decimal basePrice = 200m)
    {
        for (var drop = 20m; drop <= 400m; drop += 5m)
        {
            var prices = Enumerable.Range(0, 99).Select(i => basePrice + i).ToList();
            prices.Add(basePrice + 98 - drop);

            var series = RsiCalculator.CalculateSeries(prices, RsiSignalEvaluator.Period);
            var current = series[^1];
            var previous = series[^2];

            if (current is not null &&
                previous is not null &&
                previous.Value > 30m &&
                current.Value <= 30m)
            {
                return prices;
            }
        }

        throw new InvalidOperationException("Unable to synthesize BUY zone-entry closes.");
    }

    /// <summary>
    /// Builds closes where previous RSI &lt; 70 and current RSI ≥ 70 (sell zone entry).
    /// </summary>
    private static List<decimal> BuildZoneEntrySellCloses()
    {
        for (var rise = 20m; rise <= 400m; rise += 5m)
        {
            var prices = Enumerable.Range(0, 99).Select(i => 400m - i).ToList();
            prices.Add(400m - 98 + rise);

            var series = RsiCalculator.CalculateSeries(prices, RsiSignalEvaluator.Period);
            var current = series[^1];
            var previous = series[^2];

            if (current is not null &&
                previous is not null &&
                previous.Value < 70m &&
                current.Value >= 70m)
            {
                return prices;
            }
        }

        throw new InvalidOperationException("Unable to synthesize SELL zone-entry closes.");
    }

    private static List<decimal> BuildNeutralClosesFrom(IReadOnlyList<decimal> previous)
    {
        // Mild upward drift from last price to leave buy zone without hitting sell.
        var last = previous[^1];
        var prices = new List<decimal>();
        for (var i = 0; i < 100; i++)
            prices.Add(last + 50m + i * 0.5m);

        // Ensure last RSI is between thresholds.
        var series = RsiCalculator.CalculateSeries(prices, RsiSignalEvaluator.Period);
        var rsi = series[^1];
        if (rsi is null || rsi <= 30m || rsi >= 70m)
        {
            // Fallback flat mid-range prices.
            return Enumerable.Range(0, 100).Select(_ => 150m).ToList();
        }

        return prices;
    }

    // ── Infrastructure ──────────────────────────────────────────

    private static ServiceProvider CreateServiceProvider(decimal seedUserBalance)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var databaseName = $"BotMonitorTestDb-{Guid.NewGuid()}";
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<IBotAutoTradeExecutor, BotAutoTradeExecutor>();

        var sp = services.BuildServiceProvider();

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.Users.Any(u => u.Id == 1))
        {
            db.Users.Add(new User
            {
                Id = 1, Username = "user1", Email = "u1@test.com", PasswordHash = "h",
                VirtualBalance = seedUserBalance
            });
            db.SaveChanges();
        }

        return sp;
    }

    private static async Task<int> SeedBotAsync(
        ServiceProvider sp, int userId, decimal quantity)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bot = new TradingBot
        {
            UserId = userId,
            Symbol = "BTCUSDT",
            IsActive = true,
            BuyRsiThreshold = 30m,
            SellRsiThreshold = 70m,
            TradeQuantity = quantity
        };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();
        return bot.Id;
    }

    private static async Task SeedHoldingAsync(
        ServiceProvider sp, int userId, string symbol, decimal qty, decimal avg)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PortfolioHoldings.Add(new PortfolioHolding
        {
            UserId = userId, Symbol = symbol, Quantity = qty, AvgBuyPrice = avg
        });
        await db.SaveChangesAsync();
    }

    private static Mock<IBinanceKlineService> CreateKlineMock(IReadOnlyList<decimal> closingPrices)
    {
        var mock = new Mock<IBinanceKlineService>();
        mock.Setup(s => s.GetClosingPricesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(closingPrices);
        return mock;
    }

    private static BotMonitorService CreateMonitorService(
        ServiceProvider serviceProvider,
        IBinanceKlineService klineService)
    {
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = serviceProvider.GetRequiredService<ILogger<BotMonitorService>>();
        var botOptions = Options.Create(new TradingBotOptions { SignalExpirationMinutes = 15 });
        return new BotMonitorService(scopeFactory, klineService, botOptions, logger);
    }

    private static async Task EvaluateBotsAsync(BotMonitorService monitorService)
    {
        var evaluateMethod = typeof(BotMonitorService).GetMethod(
            "EvaluateBotsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(evaluateMethod);

        var evaluationTask = (Task?)evaluateMethod.Invoke(
            monitorService,
            new object[] { CancellationToken.None });

        Assert.NotNull(evaluationTask);
        await evaluationTask;
    }
}
