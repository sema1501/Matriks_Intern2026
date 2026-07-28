using System.Reflection;
using CryptoTracker.API.Data;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace CryptoTracker.API.Tests;

public class BotMonitorServiceTests
{
    [Fact]
    public async Task MultipleBotsWithSameSymbol_RequestKlinesOnlyOnce()
    {
        // Arrange
        await using var serviceProvider = CreateServiceProvider();

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            context.TradingBots.AddRange(
                new TradingBot
                {
                    UserId = 101,
                    Symbol = "BTCUSDT",
                    IsActive = true,
                    BuyRsiThreshold = 30m,
                    SellRsiThreshold = 70m,
                    TradeQuantity = 0.01m
                },
                new TradingBot
                {
                    UserId = 102,
                    Symbol = " btcusdt ",
                    IsActive = true,
                    BuyRsiThreshold = 30m,
                    SellRsiThreshold = 70m,
                    TradeQuantity = 0.02m
                });

            await context.SaveChangesAsync();
        }

        var closingPrices = Enumerable
            .Range(1, 100)
            .Select(price => (decimal)price)
            .ToList();

        var klineServiceMock = CreateKlineServiceMock(closingPrices);

        var monitorService =
            CreateMonitorService(serviceProvider, klineServiceMock.Object);

        // Act
        await EvaluateBotsAsync(monitorService);

        // Assert
        klineServiceMock.Verify(
            service => service.GetClosingPricesAsync(
                "BTCUSDT",
                "1m",
                100,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuyThresholdReached_CreatesPendingBuySignal()
    {
        // Arrange
        await using var serviceProvider = CreateServiceProvider();

        int botId;

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var bot = new TradingBot
            {
                UserId = 201,
                Symbol = "ETHUSDT",
                IsActive = true,
                BuyRsiThreshold = 30m,
                SellRsiThreshold = 70m,
                TradeQuantity = 0.10m
            };

            context.TradingBots.Add(bot);
            await context.SaveChangesAsync();

            botId = bot.Id;
        }

        // Sürekli düşen fiyatlar RSI değerini alış eşiğine indirir.
        var closingPrices = Enumerable
            .Range(1, 100)
            .Reverse()
            .Select(price => (decimal)price)
            .ToList();

        var klineServiceMock = CreateKlineServiceMock(closingPrices);

        var monitorService =
            CreateMonitorService(serviceProvider, klineServiceMock.Object);

        // Act
        await EvaluateBotsAsync(monitorService);

        // Assert
        await using var assertionScope =
            serviceProvider.CreateAsyncScope();

        var assertionContext =
            assertionScope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var signal = await assertionContext.BotSignals.SingleAsync();

        Assert.Equal(botId, signal.BotId);
        Assert.Equal(BotSignalType.Buy, signal.SignalType);
        Assert.Equal(BotSignalStatus.Pending, signal.Status);
        Assert.Equal(closingPrices[^1], signal.PriceAtSignal);
        Assert.True(signal.RsiValueAtSignal <= 30m);
    }

    [Fact]
    public async Task PendingSignalAlreadyExists_DoesNotCreateDuplicateSignal()
    {
        // Arrange
        await using var serviceProvider = CreateServiceProvider();

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var bot = new TradingBot
            {
                UserId = 301,
                Symbol = "BNBUSDT",
                IsActive = true,
                BuyRsiThreshold = 30m,
                SellRsiThreshold = 70m,
                TradeQuantity = 0.25m
            };

            context.TradingBots.Add(bot);
            await context.SaveChangesAsync();

            context.BotSignals.Add(new BotSignal
            {
                BotId = bot.Id,
                SignalType = BotSignalType.Buy,
                RsiValueAtSignal = 25m,
                PriceAtSignal = 500m,
                CreatedAt = DateTime.UtcNow,
                Status = BotSignalStatus.Pending
            });

            await context.SaveChangesAsync();
        }

        var closingPrices = Enumerable
            .Range(1, 100)
            .Reverse()
            .Select(price => (decimal)price)
            .ToList();

        var klineServiceMock = CreateKlineServiceMock(closingPrices);

        var monitorService =
            CreateMonitorService(serviceProvider, klineServiceMock.Object);

        // Act
        await EvaluateBotsAsync(monitorService);

        // Assert
        await using var assertionScope =
            serviceProvider.CreateAsyncScope();

        var assertionContext =
            assertionScope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var signals = await assertionContext.BotSignals.ToListAsync();

        Assert.Single(signals);
        Assert.Equal(BotSignalType.Buy, signals[0].SignalType);
        Assert.Equal(BotSignalStatus.Pending, signals[0].Status);
    }

    [Fact]
    public async Task InactiveBot_IsNotEvaluated()
    {
        // Arrange
        await using var serviceProvider = CreateServiceProvider();

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            context.TradingBots.Add(new TradingBot
            {
                UserId = 401,
                Symbol = "SOLUSDT",
                IsActive = false,
                BuyRsiThreshold = 30m,
                SellRsiThreshold = 70m,
                TradeQuantity = 1m
            });

            await context.SaveChangesAsync();
        }

        var closingPrices = Enumerable
            .Range(1, 100)
            .Select(price => (decimal)price)
            .ToList();

        var klineServiceMock = CreateKlineServiceMock(closingPrices);

        var monitorService =
            CreateMonitorService(serviceProvider, klineServiceMock.Object);

        // Act
        await EvaluateBotsAsync(monitorService);

        // Assert
        klineServiceMock.Verify(
            service => service.GetClosingPricesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        await using var assertionScope =
            serviceProvider.CreateAsyncScope();

        var assertionContext =
            assertionScope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        Assert.Empty(await assertionContext.BotSignals.ToListAsync());
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        var databaseName =
            $"BotMonitorTestDb-{Guid.NewGuid()}";

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        return services.BuildServiceProvider();
    }

    private static Mock<IBinanceKlineService> CreateKlineServiceMock(
        IReadOnlyList<decimal> closingPrices)
    {
        var mock = new Mock<IBinanceKlineService>();

        mock.Setup(service => service.GetClosingPricesAsync(
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
        var scopeFactory =
            serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var logger =
            serviceProvider.GetRequiredService<
                ILogger<BotMonitorService>>();

        return new BotMonitorService(
            scopeFactory,
            klineService,
            logger);
    }

    private static async Task EvaluateBotsAsync(
        BotMonitorService monitorService)
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