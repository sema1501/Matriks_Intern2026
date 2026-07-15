using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Tests;

public class AlertServiceTests
{
    [Fact]
    public async Task Create_rejects_unsupported_interval()
    {
        await using var db = CreateDb();
        var service = new AlertService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(1, new CreateAlertRequest("BTCUSDT", 100m, AlertDirection.Above, AlertInterval.Hourly)));

        Assert.Equal(AlertConditionEvaluator.UnsupportedIntervalMessage, ex.Message);
    }

    [Fact]
    public async Task User_can_read_own_signals()
    {
        await using var db = CreateDb();
        var alert = await SeedAlertAsync(db, userId: 1);
        db.AlertSignals.Add(new AlertSignal
        {
            AlertId = alert.Id,
            PriceAtTrigger = 100m,
            TriggeredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new AlertService(db);
        var result = await service.GetSignalsAsync(1, alert.Id);

        Assert.Equal(alert.Id, result.AlertId);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Signals);
    }

    [Fact]
    public async Task User_cannot_read_another_users_signals()
    {
        await using var db = CreateDb();
        var alert = await SeedAlertAsync(db, userId: 1);

        var service = new AlertService(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetSignalsAsync(2, alert.Id));
    }

    [Fact]
    public async Task User_can_deactivate_own_alert()
    {
        await using var db = CreateDb();
        var alert = await SeedAlertAsync(db, userId: 1);
        var service = new AlertService(db);

        var updated = await service.ToggleAsync(1, alert.Id, new ToggleAlertRequest(false));

        Assert.False(updated.IsActive);
        Assert.False((await db.PriceAlerts.FindAsync(alert.Id))!.IsActive);
    }

    [Fact]
    public async Task User_cannot_toggle_another_users_alert()
    {
        await using var db = CreateDb();
        var alert = await SeedAlertAsync(db, userId: 1);
        var service = new AlertService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ToggleAsync(2, alert.Id, new ToggleAlertRequest(false)));
    }

    [Fact]
    public async Task Alert_and_signals_persist_across_dbcontext_instances()
    {
        var dbName = Guid.NewGuid().ToString();
        int alertId;

        await using (var db = CreateDb(dbName))
        {
            var service = new AlertService(db);
            var created = await service.CreateAsync(
                1,
                new CreateAlertRequest("BTCUSDT", 10m, AlertDirection.Above, AlertInterval.Minute));
            alertId = created.Id;

            db.AlertSignals.Add(new AlertSignal
            {
                AlertId = alertId,
                PriceAtTrigger = 20m,
                TriggeredAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDb(dbName))
        {
            var service = new AlertService(db);
            var alerts = (await service.GetByUserAsync(1)).ToList();
            var signals = await service.GetSignalsAsync(1, alertId);

            Assert.Single(alerts);
            Assert.True(alerts[0].IsActive);
            Assert.Equal(1, alerts[0].SignalCount);
            Assert.Equal(1, signals.TotalCount);
        }
    }

    private static AppDbContext CreateDb(string? name = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<PriceAlert> SeedAlertAsync(AppDbContext db, int userId)
    {
        var alert = new PriceAlert
        {
            UserId = userId,
            Symbol = "BTCUSDT",
            TargetPrice = 100m,
            Direction = AlertDirection.Above,
            IsActive = true,
            Interval = AlertInterval.Minute,
            CreatedAt = DateTime.UtcNow
        };
        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync();
        return alert;
    }
}
