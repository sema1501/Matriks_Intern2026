using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
using Moq;

namespace CryptoTracker.API.Tests;

public class BotBacktestIntegrationTests : IClassFixture<BotBacktestApiFactory>
{
    private readonly BotBacktestApiFactory _factory;

    public BotBacktestIntegrationTests(BotBacktestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Backtest_AuthenticatedOwner_Returns200()
    {
        var botId = await SeedBotAsync(ownerUserId: 1);
        var client = CreateClientForUser(1);

        var start = DateTime.UtcNow.AddHours(-2);
        var end = DateTime.UtcNow.AddHours(-1);

        var response = await client.PostAsJsonAsync(
            $"/api/Bot/{botId}/backtest",
            new BacktestRequestDto(start, end));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<BacktestResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(botId, body.BotId);
        Assert.Equal("BTCUSDT", body.Symbol);
        Assert.Equal("RSI", body.Strategy);
        Assert.Equal("1m", body.Interval);
        Assert.NotNull(body.Summary);
        Assert.NotNull(body.Signals);
    }

    [Fact]
    public async Task Backtest_MalformedDateRange_Returns400()
    {
        var botId = await SeedBotAsync(ownerUserId: 1);
        var client = CreateClientForUser(1);

        var when = DateTime.UtcNow.AddDays(-1);
        var response = await client.PostAsJsonAsync(
            $"/api/Bot/{botId}/backtest",
            new BacktestRequestDto(when, when));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Backtest_MissingBot_Returns404()
    {
        var client = CreateClientForUser(1);

        var response = await client.PostAsJsonAsync(
            "/api/Bot/99999/backtest",
            new BacktestRequestDto(
                DateTime.UtcNow.AddDays(-2),
                DateTime.UtcNow.AddDays(-1)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Backtest_OtherUsersBot_Returns404()
    {
        var botId = await SeedBotAsync(ownerUserId: 1);
        var client = CreateClientForUser(2);

        var response = await client.PostAsJsonAsync(
            $"/api/Bot/{botId}/backtest",
            new BacktestRequestDto(
                DateTime.UtcNow.AddDays(-2),
                DateTime.UtcNow.AddDays(-1)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClientForUser(int userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private async Task<int> SeedBotAsync(int ownerUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.Users.AnyAsync(u => u.Id == ownerUserId))
        {
            db.Users.Add(new User
            {
                Id = ownerUserId,
                Username = $"user{ownerUserId}",
                Email = $"u{ownerUserId}@test.com",
                PasswordHash = "h"
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync(u => u.Id == 2))
        {
            db.Users.Add(new User
            {
                Id = 2,
                Username = "user2",
                Email = "u2@test.com",
                PasswordHash = "h"
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

public class BotBacktestApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

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

            // Replace historical market data with a deterministic in-memory series (no live Binance).
            var klineMock = new Mock<IBinanceKlineService>();
            klineMock.Setup(s => s.GetHistoricalKlinesAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string _, DateTime from, DateTime to, CancellationToken _) =>
                {
                    var fromUtc = from.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(from, DateTimeKind.Utc)
                        : from.ToUniversalTime();
                    var toUtc = to.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(to, DateTimeKind.Utc)
                        : to.ToUniversalTime();

                    var list = new List<BinanceKlineCandle>();
                    var cursor = fromUtc;
                    var i = 0;
                    while (cursor <= toUtc && i < 5000)
                    {
                        list.Add(new BinanceKlineCandle(cursor, 1000m - i));
                        cursor = cursor.AddMinutes(1);
                        i++;
                    }

                    return list;
                });

            klineMock.Setup(s => s.GetClosingPricesAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Range(1, 100).Select(i => (decimal)i).ToList());

            foreach (var descriptor in services
                         .Where(d => d.ServiceType == typeof(IBinanceKlineService))
                         .ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IBinanceKlineService>(klineMock.Object);

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }
}
