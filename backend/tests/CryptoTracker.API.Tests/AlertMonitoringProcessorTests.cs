using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CryptoTracker.API.Tests;

public class AlertMonitoringProcessorTests
{
    [Fact]
    public async Task Inactive_alerts_are_ignored()
    {
        await using var db = CreateDb();
        db.PriceAlerts.Add(CreateAlert(userId: 1, symbol: "BTCUSDT", target: 1m, direction: AlertDirection.Above, isActive: false));
        await db.SaveChangesAsync();

        var binance = new Mock<IBinancePriceService>(MockBehavior.Strict);
        var processor = new AlertMonitoringProcessor(NullLogger<AlertMonitoringProcessor>.Instance);

        var result = await processor.ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(0, result.ActiveAlertCount);
        Assert.Equal(0, result.GeneratedSignalCount);
        Assert.Empty(db.AlertSignals);
        binance.Verify(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Multiple_alerts_same_symbol_request_prices_once()
    {
        await using var db = CreateDb();
        db.PriceAlerts.AddRange(
            CreateAlert(1, "BTCUSDT", 1m, AlertDirection.Above),
            CreateAlert(1, "BTCUSDT", 2m, AlertDirection.Above),
            CreateAlert(2, "ETHUSDT", 1m, AlertDirection.Above));
        await db.SaveChangesAsync();

        var requested = new List<IReadOnlyCollection<string>>();
        var binance = new Mock<IBinancePriceService>();
        binance
            .Setup(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> symbols, CancellationToken _) =>
            {
                var list = symbols.ToList();
                requested.Add(list);
                return list.ToDictionary(s => s, _ => 100m, StringComparer.Ordinal);
            });

        var processor = new AlertMonitoringProcessor(NullLogger<AlertMonitoringProcessor>.Instance);
        var result = await processor.ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Single(requested);
        Assert.Equal(2, requested[0].Count);
        Assert.Contains("BTCUSDT", requested[0]);
        Assert.Contains("ETHUSDT", requested[0]);
        Assert.Equal(2, result.UniqueSymbolCount);
        Assert.Equal(3, result.GeneratedSignalCount);
        binance.Verify(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Satisfied_condition_creates_signal_every_cycle()
    {
        await using var db = CreateDb();
        db.PriceAlerts.Add(CreateAlert(1, "BTCUSDT", 50m, AlertDirection.Above));
        await db.SaveChangesAsync();

        var binance = new Mock<IBinancePriceService>();
        binance
            .Setup(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal> { ["BTCUSDT"] = 100m });

        var processor = new AlertMonitoringProcessor(NullLogger<AlertMonitoringProcessor>.Instance);

        await processor.ProcessAsync(db, binance.Object, CancellationToken.None);
        await processor.ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(2, await db.AlertSignals.CountAsync());
    }

    [Fact]
    public async Task Existing_signals_do_not_suppress_new_signals()
    {
        await using var db = CreateDb();
        var alert = CreateAlert(1, "BTCUSDT", 50m, AlertDirection.Above);
        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync();

        db.AlertSignals.Add(new AlertSignal
        {
            AlertId = alert.Id,
            PriceAtTrigger = 80m,
            TriggeredAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        var binance = new Mock<IBinancePriceService>();
        binance
            .Setup(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal> { ["BTCUSDT"] = 90m });

        var processor = new AlertMonitoringProcessor(NullLogger<AlertMonitoringProcessor>.Instance);
        await processor.ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(2, await db.AlertSignals.CountAsync());
    }

    [Fact]
    public async Task Missing_binance_prices_do_not_crash_cycle()
    {
        await using var db = CreateDb();
        db.PriceAlerts.Add(CreateAlert(1, "MISSINGUSDT", 1m, AlertDirection.Above));
        await db.SaveChangesAsync();

        var binance = new Mock<IBinancePriceService>();
        binance
            .Setup(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal>());

        var processor = new AlertMonitoringProcessor(NullLogger<AlertMonitoringProcessor>.Instance);
        var result = await processor.ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(0, result.GeneratedSignalCount);
        Assert.Equal(1, result.SkippedAlertCount);
        Assert.Empty(db.AlertSignals);
    }

    [Fact]
    public async Task Deactivated_alert_is_excluded_from_monitoring()
    {
        await using var db = CreateDb();
        var alert = CreateAlert(1, "BTCUSDT", 1m, AlertDirection.Above, isActive: true);
        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync();

        var service = new AlertService(db);
        await service.ToggleAsync(1, alert.Id, new ToggleAlertRequest(false));

        var binance = new Mock<IBinancePriceService>(MockBehavior.Strict);
        var processor = new AlertMonitoringProcessor(NullLogger<AlertMonitoringProcessor>.Instance);
        var result = await processor.ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(0, result.ActiveAlertCount);
        binance.Verify(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static PriceAlert CreateAlert(
        int userId,
        string symbol,
        decimal target,
        AlertDirection direction,
        bool isActive = true) => new()
    {
        UserId = userId,
        Symbol = symbol,
        TargetPrice = target,
        Direction = direction,
        IsActive = isActive,
        Interval = AlertInterval.Minute,
        CreatedAt = DateTime.UtcNow
    };
}
