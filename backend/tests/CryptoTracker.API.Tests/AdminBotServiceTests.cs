using CryptoTracker.API.Data;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Tests;

public class AdminBotServiceTests
{
    private const int OwnerUserId = 1;
    private const int AdminUserId = 10;

    [Fact]
    public async Task ForceStop_DeactivatesBot_AndWritesAuditWithAuthenticatedActor()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var botId = await SeedBotAsync(db, OwnerUserId, "BTCUSDT");
        var clock = new FrozenClock(new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc));
        var service = new AdminBotService(db, clock);

        await service.ForceStopAsync(AdminUserId, botId, "Aşırı emir", CancellationToken.None);

        var bot = await db.TradingBots.AsNoTracking().SingleAsync(b => b.Id == botId);
        Assert.False(bot.IsActive);

        var log = await db.AuditLogs.SingleAsync();
        Assert.Equal(AdminUserId, log.ActorUserId);
        Assert.Equal(AuditLogActions.BotForceStopped, log.Action);
        Assert.Equal(botId, log.TargetId);
        Assert.Equal(clock.UtcNow, log.CreatedAt);
        Assert.Contains("BTCUSDT", log.Details);
        Assert.Contains("Aşırı emir", log.Details);
        Assert.DoesNotContain("password", log.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", log.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Flag_MarksBot_AndWritesAuditWithAuthenticatedActor()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var botId = await SeedBotAsync(db, OwnerUserId, "ETHUSDT");
        var service = new AdminBotService(db, new FrozenClock(DateTime.UtcNow));

        await service.FlagAsync(AdminUserId, botId, "Rate limit riski", CancellationToken.None);

        var bot = await db.TradingBots.AsNoTracking().SingleAsync(b => b.Id == botId);
        Assert.True(bot.IsFlagged);

        var log = await db.AuditLogs.SingleAsync();
        Assert.Equal(AdminUserId, log.ActorUserId);
        Assert.Equal(AuditLogActions.BotFlagged, log.Action);
        Assert.Equal(botId, log.TargetId);
        Assert.Contains("ETHUSDT", log.Details);
        Assert.Contains("Rate limit riski", log.Details);
    }

    [Fact]
    public async Task ForceStop_MissingBot_DoesNotWriteAudit()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var service = new AdminBotService(db, new FrozenClock(DateTime.UtcNow));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ForceStopAsync(AdminUserId, botId: 999, "note", CancellationToken.None));

        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task Flag_MissingBot_DoesNotWriteAudit()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var service = new AdminBotService(db, new FrozenClock(DateTime.UtcNow));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.FlagAsync(AdminUserId, botId: 999, "note", CancellationToken.None));

        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task ForceStop_DoesNotUseBotOwnerAsActor()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var botId = await SeedBotAsync(db, OwnerUserId, "SOLUSDT");
        var service = new AdminBotService(db, new FrozenClock(DateTime.UtcNow));

        await service.ForceStopAsync(AdminUserId, botId, null, CancellationToken.None);

        var log = await db.AuditLogs.SingleAsync();
        Assert.Equal(AdminUserId, log.ActorUserId);
        Assert.NotEqual(OwnerUserId, log.ActorUserId);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        db.Users.AddRange(
            new User { Id = OwnerUserId, Username = "owner", Email = "owner@t.com", PasswordHash = "h" },
            new User { Id = AdminUserId, Username = "admin", Email = "admin@t.com", PasswordHash = "h" });
        await db.SaveChangesAsync();
    }

    private static async Task<int> SeedBotAsync(AppDbContext db, int userId, string symbol)
    {
        var bot = new TradingBot
        {
            UserId = userId,
            Symbol = symbol,
            TradeQuantity = 1m,
            IsActive = true
        };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();
        return bot.Id;
    }

    private sealed class FrozenClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
