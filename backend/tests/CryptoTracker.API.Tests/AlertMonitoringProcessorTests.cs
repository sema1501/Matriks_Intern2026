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
    private static readonly DateTime T0 = new(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Inactive_alerts_are_ignored()
    {
        await using var db = CreateDb();
        db.PriceAlerts.Add(CreateAlert(userId: 1, symbol: "BTCUSDT", target: 1m, direction: AlertDirection.Above, isActive: false));
        await db.SaveChangesAsync();

        var binance = new Mock<IBinancePriceService>(MockBehavior.Strict);
        var processor = CreateProcessor(T0);

        var result = await processor.ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(0, result.ActiveAlertCount);
        Assert.Equal(0, result.GeneratedSignalCount);
        Assert.Empty(db.AlertSignals);
        binance.Verify(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Inactive_hourly_alert_is_never_due_even_when_LastCheckedAt_is_null()
    {
        await using var db = CreateDb();
        db.PriceAlerts.Add(CreateAlert(1, "BTCUSDT", 1m, AlertDirection.Above, isActive: false, interval: AlertInterval.Hourly));
        await db.SaveChangesAsync();

        var binance = new Mock<IBinancePriceService>(MockBehavior.Strict);
        var result = await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(0, result.ActiveAlertCount);
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

        var result = await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Single(requested);
        Assert.Equal(2, requested[0].Count);
        Assert.Contains("BTCUSDT", requested[0]);
        Assert.Contains("ETHUSDT", requested[0]);
        Assert.Equal(2, result.UniqueSymbolCount);
        Assert.Equal(3, result.GeneratedSignalCount);
        binance.Verify(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Mixed_minute_and_hourly_due_same_symbol_fetches_binance_once()
    {
        await using var db = CreateDb();
        db.PriceAlerts.AddRange(
            CreateAlert(1, "BTCUSDT", 1m, AlertDirection.Above, interval: AlertInterval.Minute),
            CreateAlert(1, "BTCUSDT", 1m, AlertDirection.Above, interval: AlertInterval.Hourly));
        await db.SaveChangesAsync();

        var binance = new Mock<IBinancePriceService>();
        binance
            .Setup(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal> { ["BTCUSDT"] = 100m });

        var result = await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(2, result.ActiveAlertCount);
        Assert.Equal(1, result.UniqueSymbolCount);
        Assert.Equal(2, result.GeneratedSignalCount);
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

        var clock = new FakeClock(T0);
        var processor = CreateProcessor(clock);

        await processor.ProcessAsync(db, binance.Object, CancellationToken.None);
        clock.UtcNow = T0.AddMinutes(1);
        await processor.ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(2, await db.AlertSignals.CountAsync());
    }

    [Fact]
    public async Task Minute_alert_due_when_LastCheckedAt_null_then_not_due_until_one_minute()
    {
        await using var db = CreateDb();
        var alert = CreateAlert(1, "BTCUSDT", 50m, AlertDirection.Above);
        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync();

        var binance = SetupPrice("BTCUSDT", 100m);
        var clock = new FakeClock(T0);
        var processor = CreateProcessor(clock);

        var first = await processor.ProcessAsync(db, binance.Object, CancellationToken.None);
        Assert.Equal(1, first.GeneratedSignalCount);
        Assert.Equal(T0, (await db.PriceAlerts.FindAsync(alert.Id))!.LastCheckedAt);

        clock.UtcNow = T0.AddSeconds(30);
        var mid = await processor.ProcessAsync(db, binance.Object, CancellationToken.None);
        Assert.Equal(0, mid.ActiveAlertCount);
        Assert.Equal(0, mid.GeneratedSignalCount);
        binance.Verify(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);

        clock.UtcNow = T0.AddMinutes(1);
        var again = await processor.ProcessAsync(db, binance.Object, CancellationToken.None);
        Assert.Equal(1, again.GeneratedSignalCount);
        Assert.Equal(2, await db.AlertSignals.CountAsync());
    }

    [Fact]
    public async Task Hourly_alert_not_due_at_59_minutes_due_at_60()
    {
        await using var db = CreateDb();
        var alert = CreateAlert(1, "BTCUSDT", 50m, AlertDirection.Above, interval: AlertInterval.Hourly);
        alert.LastCheckedAt = T0;
        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync();

        var binance = SetupPrice("BTCUSDT", 100m);
        var clock = new FakeClock(T0.AddMinutes(59));

        var notDue = await CreateProcessor(clock).ProcessAsync(db, binance.Object, CancellationToken.None);
        Assert.Equal(0, notDue.ActiveAlertCount);
        Assert.Equal(0, notDue.GeneratedSignalCount);
        binance.Verify(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);

        clock.UtcNow = T0.AddHours(1);
        var due = await CreateProcessor(clock).ProcessAsync(db, binance.Object, CancellationToken.None);
        Assert.Equal(1, due.GeneratedSignalCount);
        Assert.Equal(T0.AddHours(1), (await db.PriceAlerts.FindAsync(alert.Id))!.LastCheckedAt);
    }

    [Fact]
    public async Task Daily_alert_not_due_at_23_hours_due_at_24()
    {
        await using var db = CreateDb();
        var alert = CreateAlert(1, "BTCUSDT", 50m, AlertDirection.Above, interval: AlertInterval.Daily);
        alert.LastCheckedAt = T0;
        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync();

        var binance = SetupPrice("BTCUSDT", 100m);
        var clock = new FakeClock(T0.AddHours(23));

        var notDue = await CreateProcessor(clock).ProcessAsync(db, binance.Object, CancellationToken.None);
        Assert.Equal(0, notDue.ActiveAlertCount);
        binance.Verify(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);

        clock.UtcNow = T0.AddDays(1);
        var due = await CreateProcessor(clock).ProcessAsync(db, binance.Object, CancellationToken.None);
        Assert.Equal(1, due.GeneratedSignalCount);
    }

    [Fact]
    public async Task Unknown_interval_is_skipped_without_stopping_other_alerts()
    {
        await using var db = CreateDb();
        db.PriceAlerts.AddRange(
            CreateAlert(1, "BTCUSDT", 50m, AlertDirection.Above, interval: (AlertInterval)99),
            CreateAlert(1, "ETHUSDT", 50m, AlertDirection.Above, interval: AlertInterval.Minute));
        await db.SaveChangesAsync();

        var binance = new Mock<IBinancePriceService>();
        binance
            .Setup(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal>
            {
                ["BTCUSDT"] = 100m,
                ["ETHUSDT"] = 100m
            });

        var result = await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(1, result.ActiveAlertCount);
        Assert.Equal(1, result.GeneratedSignalCount);
        Assert.True(result.SkippedAlertCount >= 1);
        Assert.Single(db.AlertSignals);
        Assert.Equal("ETHUSDT", db.PriceAlerts.Single(a => a.LastCheckedAt != null).Symbol);
        binance.Verify(
            b => b.GetPricesAsync(It.Is<IEnumerable<string>>(s => s.Single() == "ETHUSDT"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LastCheckedAt_persists_across_dbcontext_instances()
    {
        var dbName = Guid.NewGuid().ToString();
        int alertId;

        await using (var db = CreateDb(dbName))
        {
            var alert = CreateAlert(1, "BTCUSDT", 50m, AlertDirection.Above);
            db.PriceAlerts.Add(alert);
            await db.SaveChangesAsync();
            alertId = alert.Id;

            var binance = SetupPrice("BTCUSDT", 100m);
            await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);
        }

        await using (var db = CreateDb(dbName))
        {
            var reloaded = await db.PriceAlerts.FindAsync(alertId);
            Assert.NotNull(reloaded);
            Assert.Equal(T0, reloaded.LastCheckedAt);
        }
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
            TriggeredAt = T0.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        var binance = SetupPrice("BTCUSDT", 90m);
        await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);

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

        var result = await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(0, result.GeneratedSignalCount);
        Assert.Equal(1, result.SkippedAlertCount);
        Assert.Empty(db.AlertSignals);
        Assert.Null((await db.PriceAlerts.SingleAsync()).LastCheckedAt);
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
        var result = await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(0, result.ActiveAlertCount);
        binance.Verify(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AlertMonitoringProcessor CreateProcessor(DateTime utcNow) =>
        CreateProcessor(new FakeClock(utcNow));

    private static AlertMonitoringProcessor CreateProcessor(FakeClock clock) =>
        new(NullLogger<AlertMonitoringProcessor>.Instance, clock);

    private static Mock<IBinancePriceService> SetupPrice(string symbol, decimal price)
    {
        var binance = new Mock<IBinancePriceService>();
        binance
            .Setup(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal> { [symbol] = price });
        return binance;
    }

    private static AppDbContext CreateDb(string? name = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static PriceAlert CreateAlert(
        int userId,
        string symbol,
        decimal target,
        AlertDirection direction,
        bool isActive = true,
        AlertInterval interval = AlertInterval.Minute) => new()
    {
        UserId = userId,
        Symbol = symbol,
        TargetPrice = target,
        Direction = direction,
        IsActive = isActive,
        Interval = interval,
        CreatedAt = T0
    };

    private sealed class FakeClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
