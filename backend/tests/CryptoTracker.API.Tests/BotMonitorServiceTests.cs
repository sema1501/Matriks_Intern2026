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
        var portfolioMock = new Mock<IPortfolioService>();
        await using var serviceProvider = CreateServiceProvider(portfolioMock.Object);

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

        await EvaluateBotsAsync(monitorService);

        klineServiceMock.Verify(
            service => service.GetClosingPricesAsync(
                "BTCUSDT",
                "1m",
                100,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuyThresholdReached_ExecutesBuyOrderDirectly()
    {
        var portfolioMock = new Mock<IPortfolioService>();
        await using var serviceProvider = CreateServiceProvider(portfolioMock.Object);

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            context.TradingBots.Add(new TradingBot
            {
                UserId = 201,
                Symbol = "ETHUSDT",
                IsActive = true,
                BuyRsiThreshold = 30m,
                SellRsiThreshold = 70m,
                TradeQuantity = 0.10m
            });

            await context.SaveChangesAsync();
        }

        // Sürekli düşen fiyatlar RSI değerini alış eşiğine indirir.
        var closingPrices = Enumerable
            .Range(1, 100)
            .Reverse()
            .Select(price => (decimal)price)
            .ToList();

        var expectedPrice = closingPrices[closingPrices.Count - 1];

        var klineServiceMock = CreateKlineServiceMock(closingPrices);

        var monitorService =
            CreateMonitorService(serviceProvider, klineServiceMock.Object);

        await EvaluateBotsAsync(monitorService);

        // Onay beklemeden doğrudan alım emri gönderilmeli
        portfolioMock.Verify(
            service => service.BuyAsync(
                201,
                "ETHUSDT",
                0.10m,
                expectedPrice,
                It.IsAny<CancellationToken>()),
            Times.Once);

        portfolioMock.Verify(
            service => service.SellAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SellThresholdReached_ExecutesSellOrderDirectly()
    {
        var portfolioMock = new Mock<IPortfolioService>();
        await using var serviceProvider = CreateServiceProvider(portfolioMock.Object);

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            context.TradingBots.Add(new TradingBot
            {
                UserId = 202,
                Symbol = "BNBUSDT",
                IsActive = true,
                BuyRsiThreshold = 30m,
                SellRsiThreshold = 70m,
                TradeQuantity = 0.25m
            });

            await context.SaveChangesAsync();
        }

        // Sürekli yükselen fiyatlar RSI değerini satış eşiğine çıkarır.
        var closingPrices = Enumerable
            .Range(1, 100)
            .Select(price => (decimal)price)
            .ToList();

        var expectedPrice = closingPrices[closingPrices.Count - 1];

        var klineServiceMock = CreateKlineServiceMock(closingPrices);

        var monitorService =
            CreateMonitorService(serviceProvider, klineServiceMock.Object);

        await EvaluateBotsAsync(monitorService);

        portfolioMock.Verify(
            service => service.SellAsync(
                202,
                "BNBUSDT",
                0.25m,
                expectedPrice,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SignalTriggered_DoesNotCreatePendingSignal()
    {
        var portfolioMock = new Mock<IPortfolioService>();
        await using var serviceProvider = CreateServiceProvider(portfolioMock.Object);

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            context.TradingBots.Add(new TradingBot
            {
                UserId = 301,
                Symbol = "SOLUSDT",
                IsActive = true,
                BuyRsiThreshold = 30m,
                SellRsiThreshold = 70m,
                TradeQuantity = 1m
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

        await EvaluateBotsAsync(monitorService);

        // Onay akışı kaldırıldığı için Pending kayıt oluşmamalı
        await using var assertionScope =
            serviceProvider.CreateAsyncScope();

        var assertionContext =
            assertionScope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        Assert.Empty(await assertionContext.BotSignals.ToListAsync());
    }

    [Fact]
    public async Task OrderFails_DoesNotStopOtherBots()
    {
        var portfolioMock = new Mock<IPortfolioService>();

        portfolioMock
            .Setup(service => service.BuyAsync(
                401,
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Binance emri başarısız."));

        await using var serviceProvider = CreateServiceProvider(portfolioMock.Object);

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            context.TradingBots.AddRange(
                new TradingBot
                {
                    UserId = 401,
                    Symbol = "ADAUSDT",
                    IsActive = true,
                    BuyRsiThreshold = 30m,
                    SellRsiThreshold = 70m,
                    TradeQuantity = 5m
                },
                new TradingBot
                {
                    UserId = 402,
                    Symbol = "ADAUSDT",
                    IsActive = true,
                    BuyRsiThreshold = 30m,
                    SellRsiThreshold = 70m,
                    TradeQuantity = 7m
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

        await EvaluateBotsAsync(monitorService);

        // İlk bot hata verse de ikinci bot işlenmeli
        portfolioMock.Verify(
            service => service.BuyAsync(
                402,
                "ADAUSDT",
                7m,
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InactiveBot_IsNotEvaluated()
    {
        var portfolioMock = new Mock<IPortfolioService>();
        await using var serviceProvider = CreateServiceProvider(portfolioMock.Object);

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var context =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            context.TradingBots.Add(new TradingBot
            {
                UserId = 501,
                Symbol = "XRPUSDT",
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

        await EvaluateBotsAsync(monitorService);

        klineServiceMock.Verify(
            service => service.GetClosingPricesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        portfolioMock.Verify(
            service => service.BuyAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ServiceProvider CreateServiceProvider(
        IPortfolioService portfolioService)
    {
        var services = new ServiceCollection();

        services.AddLogging();

        var databaseName =
            $"BotMonitorTestDb-{Guid.NewGuid()}";

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        services.AddSingleton(portfolioService);

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
            serviceProvider.GetRequiredService<ILogger<BotMonitorService>>();

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