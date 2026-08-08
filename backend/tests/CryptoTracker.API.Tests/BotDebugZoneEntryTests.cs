using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CryptoTracker.API.Tests;

public class BotDebugZoneEntryTests
{
    private const int User1 = 1;
    private const int User2 = 2;

    [Fact]
    public async Task ZoneEntry_Buy_ExecutesVirtualBuy_Approves_LeavesBotActive()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = await SeedBotAsync(db, User1, quantity: 1m);

        var service = CreateService(db, price: 100m);
        var result = await service.EvaluateZoneEntryAsync(
            User1,
            bot.Id,
            new DebugZoneEntryRequest(PreviousRsi: 31m, CurrentRsi: 29m));

        Assert.True(result.SignalDetected);
        Assert.Equal(BotSignalType.Buy, result.SignalType);
        Assert.NotNull(result.Signal);
        Assert.Equal(BotSignalStatus.Approved, result.Signal.Status);
        Assert.Equal(29m, result.Signal.RsiValueAtSignal);
        Assert.Equal(100m, result.Signal.PriceAtSignal);
        Assert.True(result.BotIsActive);

        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == User1);
        Assert.Equal(10_000m - 100m, user.VirtualBalance);
        Assert.Equal(1m, (await db.PortfolioHoldings.SingleAsync()).Quantity);
        Assert.Single(await db.Transactions.ToListAsync());

        var reloaded = await db.TradingBots.AsNoTracking().SingleAsync(b => b.Id == bot.Id);
        Assert.True(reloaded.IsActive);
    }

    [Fact]
    public async Task ZoneEntry_Sell_ExecutesVirtualSell_Approves_LeavesBotActive()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        db.PortfolioHoldings.Add(new PortfolioHolding
        {
            UserId = User1, Symbol = "BTCUSDT", Quantity = 5m, AvgBuyPrice = 50m
        });
        await db.SaveChangesAsync();
        var bot = await SeedBotAsync(db, User1, quantity: 1m);

        var service = CreateService(db, price: 120m);
        var result = await service.EvaluateZoneEntryAsync(
            User1,
            bot.Id,
            new DebugZoneEntryRequest(PreviousRsi: 69m, CurrentRsi: 71m));

        Assert.True(result.SignalDetected);
        Assert.Equal(BotSignalType.Sell, result.SignalType);
        Assert.NotNull(result.Signal);
        Assert.Equal(BotSignalStatus.Approved, result.Signal.Status);
        Assert.True(result.BotIsActive);

        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == User1);
        Assert.Equal(10_000m + 120m, user.VirtualBalance);
        Assert.Equal(4m, (await db.PortfolioHoldings.SingleAsync()).Quantity);
        Assert.Single(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task ZoneEntry_NoEntry_DoesNotMutatePortfolioOrCreateSignal()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = await SeedBotAsync(db, User1, quantity: 1m);

        var service = CreateService(db, price: 100m);
        var result = await service.EvaluateZoneEntryAsync(
            User1,
            bot.Id,
            new DebugZoneEntryRequest(PreviousRsi: 29m, CurrentRsi: 28m));

        Assert.False(result.SignalDetected);
        Assert.Null(result.SignalType);
        Assert.Null(result.Signal);

        Assert.Equal(10_000m, (await db.Users.AsNoTracking().SingleAsync(u => u.Id == User1)).VirtualBalance);
        Assert.Empty(await db.PortfolioHoldings.ToListAsync());
        Assert.Empty(await db.Transactions.ToListAsync());
        Assert.Empty(await db.BotSignals.ToListAsync());
        Assert.True((await db.TradingBots.AsNoTracking().SingleAsync(b => b.Id == bot.Id)).IsActive);
    }

    [Fact]
    public async Task ZoneEntry_OtherUsersBot_ThrowsNotFound()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = await SeedBotAsync(db, User1, quantity: 1m);
        var service = CreateService(db, price: 100m);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.EvaluateZoneEntryAsync(
                User2,
                bot.Id,
                new DebugZoneEntryRequest(31m, 29m)));
    }

    [Fact]
    public async Task Integration_Development_ZoneEntryBuy_Returns200()
    {
        await using var factory = new BotDebugApiFactory(allowDebugExecute: true, price: 100m);
        var botId = await SeedBotViaFactory(factory, ownerUserId: 1);
        var client = CreateClient(factory, userId: 1);

        var response = await client.PostAsJsonAsync(
            $"/api/Bot/{botId}/debug/zone-entry",
            new DebugZoneEntryRequest(31m, 29m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DebugZoneEntryResponse>();
        Assert.NotNull(body);
        Assert.True(body.SignalDetected);
        Assert.Equal(BotSignalType.Buy, body.SignalType);
        Assert.Equal(BotSignalStatus.Approved, body.Signal!.Status);
        Assert.Contains("DEBUG", body.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Integration_Production_ZoneEntry_Returns404()
    {
        await using var factory = new BotDebugApiFactory(allowDebugExecute: false, price: 100m);
        var botId = await SeedBotViaFactory(factory, ownerUserId: 1);
        var client = CreateClient(factory, userId: 1);

        var response = await client.PostAsJsonAsync(
            $"/api/Bot/{botId}/debug/zone-entry",
            new DebugZoneEntryRequest(31m, 29m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Integration_OtherUser_ZoneEntry_Returns404()
    {
        await using var factory = new BotDebugApiFactory(allowDebugExecute: true, price: 100m);
        var botId = await SeedBotViaFactory(factory, ownerUserId: 1);
        var client = CreateClient(factory, userId: 2);

        var response = await client.PostAsJsonAsync(
            $"/api/Bot/{botId}/debug/zone-entry",
            new DebugZoneEntryRequest(31m, 29m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void DetermineZoneEntrySignal_MatchesExpectedBuySellNoEntry()
    {
        Assert.Equal(
            BotSignalType.Buy,
            RsiSignalEvaluator.DetermineZoneEntrySignal(29m, 31m, 30m, 70m));
        Assert.Equal(
            BotSignalType.Sell,
            RsiSignalEvaluator.DetermineZoneEntrySignal(71m, 69m, 30m, 70m));
        Assert.Null(
            RsiSignalEvaluator.DetermineZoneEntrySignal(28m, 29m, 30m, 70m));
    }

    // ── Helpers ─────────────────────────────────────────────────

    private static BotDebugExecuteService CreateService(AppDbContext db, decimal price) =>
        new(db, MockPrices("BTCUSDT", price).Object, new BotAutoTradeExecutor(db, new PortfolioService(db)));

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedUsersAsync(AppDbContext db)
    {
        db.Users.AddRange(
            new User { Id = User1, Username = "u1", Email = "u1@t.com", PasswordHash = "h", VirtualBalance = 10_000m },
            new User { Id = User2, Username = "u2", Email = "u2@t.com", PasswordHash = "h", VirtualBalance = 10_000m });
        await db.SaveChangesAsync();
    }

    private static async Task<TradingBot> SeedBotAsync(AppDbContext db, int userId, decimal quantity)
    {
        var bot = new TradingBot
        {
            UserId = userId,
            Symbol = "BTCUSDT",
            TradeQuantity = quantity,
            BuyRsiThreshold = 30m,
            SellRsiThreshold = 70m,
            IsActive = true
        };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();
        return bot;
    }

    private static Mock<IBinancePriceService> MockPrices(string symbol, decimal price)
    {
        var mock = new Mock<IBinancePriceService>();
        mock.Setup(s => s.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                [symbol] = price
            });
        return mock;
    }

    private static HttpClient CreateClient(BotDebugApiFactory factory, int userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private static async Task<int> SeedBotViaFactory(BotDebugApiFactory factory, int ownerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.Users.AnyAsync(u => u.Id == ownerUserId))
        {
            db.Users.Add(new User
            {
                Id = ownerUserId,
                Username = $"user{ownerUserId}",
                Email = $"u{ownerUserId}@t.com",
                PasswordHash = "h",
                VirtualBalance = 10_000m
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync(u => u.Id == 2))
        {
            db.Users.Add(new User
            {
                Id = 2, Username = "user2", Email = "u2@t.com", PasswordHash = "h",
                VirtualBalance = 10_000m
            });
            await db.SaveChangesAsync();
        }

        var bot = new TradingBot
        {
            UserId = ownerUserId,
            Symbol = "BTCUSDT",
            BuyRsiThreshold = 30m,
            SellRsiThreshold = 70m,
            TradeQuantity = 1m,
            IsActive = true
        };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();
        return bot.Id;
    }
}
