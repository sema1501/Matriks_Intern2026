using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CryptoTracker.API.Tests;

public class BotDebugExecuteTests
{
    private const int User1 = 1;
    private const int User2 = 2;

    [Fact]
    public async Task Execute_Buy_UpdatesPortfolioAndApproves_LeavesBotActive()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = await SeedBotAsync(db, User1, quantity: 1m);

        var prices = MockPrices("BTCUSDT", 100m);
        var service = new BotDebugExecuteService(
            db, prices.Object, new BotAutoTradeExecutor(db, new PortfolioService(db)));

        var result = await service.ExecuteAsync(
            User1, bot.Id, new DebugBotExecuteRequest("BUY"));

        Assert.Equal(BotSignalStatus.Approved, result.Signal.Status);
        Assert.Equal(BotSignalType.Buy, result.Signal.SignalType);
        Assert.True(result.BotIsActive);

        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == User1);
        Assert.Equal(10_000m - 100m, user.VirtualBalance);
        Assert.Single(await db.PortfolioHoldings.ToListAsync());
        Assert.Single(await db.Transactions.ToListAsync());

        var reloaded = await db.TradingBots.AsNoTracking().SingleAsync(b => b.Id == bot.Id);
        Assert.True(reloaded.IsActive);
    }

    [Fact]
    public async Task Execute_Sell_UpdatesPortfolioAndApproves_LeavesBotActive()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        db.PortfolioHoldings.Add(new PortfolioHolding
        {
            UserId = User1, Symbol = "BTCUSDT", Quantity = 5m, AvgBuyPrice = 50m
        });
        await db.SaveChangesAsync();
        var bot = await SeedBotAsync(db, User1, quantity: 1m);

        var prices = MockPrices("BTCUSDT", 120m);
        var service = new BotDebugExecuteService(
            db, prices.Object, new BotAutoTradeExecutor(db, new PortfolioService(db)));

        var result = await service.ExecuteAsync(
            User1, bot.Id, new DebugBotExecuteRequest("SELL"));

        Assert.Equal(BotSignalStatus.Approved, result.Signal.Status);
        Assert.Equal(BotSignalType.Sell, result.Signal.SignalType);
        Assert.True(result.BotIsActive);

        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == User1);
        Assert.Equal(10_000m + 120m, user.VirtualBalance);
        Assert.Equal(4m, (await db.PortfolioHoldings.SingleAsync()).Quantity);

        var reloaded = await db.TradingBots.AsNoTracking().SingleAsync(b => b.Id == bot.Id);
        Assert.True(reloaded.IsActive);
    }

    [Fact]
    public async Task Execute_InsufficientBalance_MarksFailed_LeavesBotActive()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var user = await db.Users.SingleAsync(u => u.Id == User1);
        user.VirtualBalance = 1m;
        await db.SaveChangesAsync();

        var bot = await SeedBotAsync(db, User1, quantity: 1m);
        var prices = MockPrices("BTCUSDT", 100m);
        var service = new BotDebugExecuteService(
            db, prices.Object, new BotAutoTradeExecutor(db, new PortfolioService(db)));

        var result = await service.ExecuteAsync(
            User1, bot.Id, new DebugBotExecuteRequest("BUY"));

        Assert.Equal(BotSignalStatus.Failed, result.Signal.Status);
        Assert.Empty(await db.Transactions.ToListAsync());
        Assert.Empty(await db.PortfolioHoldings.ToListAsync());

        var reloaded = await db.TradingBots.AsNoTracking().SingleAsync(b => b.Id == bot.Id);
        Assert.True(reloaded.IsActive);
        Assert.Equal(1m, (await db.Users.AsNoTracking().SingleAsync(u => u.Id == User1)).VirtualBalance);
    }

    [Fact]
    public async Task Execute_OtherUsersBot_ThrowsNotFound()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = await SeedBotAsync(db, User1, quantity: 1m);

        var service = new BotDebugExecuteService(
            db, MockPrices("BTCUSDT", 100m).Object,
            new BotAutoTradeExecutor(db, new PortfolioService(db)));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ExecuteAsync(User2, bot.Id, new DebugBotExecuteRequest("BUY")));
    }

    [Fact]
    public async Task Integration_Development_OwnerBuy_Returns200()
    {
        await using var factory = new BotDebugApiFactory(allowDebugExecute: true, price: 100m);
        var botId = await SeedBotViaFactory(factory, ownerUserId: 1);
        var client = CreateClient(factory, userId: 1);

        var response = await client.PostAsJsonAsync(
            $"/api/Bot/{botId}/debug/execute",
            new DebugBotExecuteRequest("BUY"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DebugBotExecuteResponse>();
        Assert.NotNull(body);
        Assert.Equal(BotSignalStatus.Approved, body.Signal.Status);
        Assert.True(body.BotIsActive);
        Assert.Contains("DEBUG", body.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Integration_Production_Returns404()
    {
        await using var factory = new BotDebugApiFactory(allowDebugExecute: false, price: 100m);
        var botId = await SeedBotViaFactory(factory, ownerUserId: 1);
        var client = CreateClient(factory, userId: 1);

        var response = await client.PostAsJsonAsync(
            $"/api/Bot/{botId}/debug/execute",
            new DebugBotExecuteRequest("BUY"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Integration_OtherUser_Returns404()
    {
        await using var factory = new BotDebugApiFactory(allowDebugExecute: true, price: 100m);
        var botId = await SeedBotViaFactory(factory, ownerUserId: 1);
        var client = CreateClient(factory, userId: 2);

        var response = await client.PostAsJsonAsync(
            $"/api/Bot/{botId}/debug/execute",
            new DebugBotExecuteRequest("BUY"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────

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

public sealed class BotDebugApiFactory : WebApplicationFactory<Program>
{
    private readonly bool _allowDebugExecute;
    private readonly decimal _price;
    private readonly string _dbName = Guid.NewGuid().ToString();

    public BotDebugApiFactory(bool allowDebugExecute, decimal price)
    {
        _allowDebugExecute = allowDebugExecute;
        _price = price;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services
                         .Where(d => d.ImplementationType == typeof(BotMonitorService) ||
                                     d.ImplementationType == typeof(AlertMonitorService))
                         .ToList())
            {
                services.Remove(descriptor);
            }

            foreach (var descriptor in services
                         .Where(d => d.ServiceType == typeof(IBinancePriceService) ||
                                     d.ServiceType == typeof(IDebugEndpointAccess))
                         .ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IDebugEndpointAccess>(
                new FixedDebugEndpointAccess(_allowDebugExecute));

            var priceMock = new Mock<IBinancePriceService>();
            priceMock.Setup(s => s.GetPricesAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, decimal>(StringComparer.Ordinal)
                {
                    ["BTCUSDT"] = _price
                });
            services.AddSingleton(priceMock.Object);

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }

    private sealed class FixedDebugEndpointAccess(bool allow) : IDebugEndpointAccess
    {
        public bool AllowDebugExecute { get; } = allow;
    }
}

