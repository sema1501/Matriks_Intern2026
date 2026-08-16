using CryptoTracker.API.Data;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Tests;

public class AuditLogServiceTests
{
    private static readonly DateTime T0 = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Get_DefaultOrder_IsNewestFirst()
    {
        await using var db = await SeedLogsAsync();
        var service = new AuditLogService(db);

        var result = await service.GetAsync(null, null, ascending: false);

        Assert.Equal(3, result.Count);
        Assert.Equal("newest", result[0].Details);
        Assert.Equal("middle", result[1].Details);
        Assert.Equal("oldest", result[2].Details);
    }

    [Fact]
    public async Task Get_AscendingOrder_IsOldestFirst()
    {
        await using var db = await SeedLogsAsync();
        var service = new AuditLogService(db);

        var result = await service.GetAsync(null, null, ascending: true);

        Assert.Equal("oldest", result[0].Details);
        Assert.Equal("middle", result[1].Details);
        Assert.Equal("newest", result[2].Details);
    }

    [Fact]
    public async Task Get_FromFilter_IsInclusive()
    {
        await using var db = await SeedLogsAsync();
        var service = new AuditLogService(db);

        var result = await service.GetAsync(T0, null, ascending: true);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.True(r.CreatedAt >= T0));
    }

    [Fact]
    public async Task Get_ToFilter_DateOnlyIncludesEntireUtcDay()
    {
        await using var db = await SeedLogsAsync();
        var service = new AuditLogService(db);
        var toDateOnly = new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Unspecified);

        var result = await service.GetAsync(null, toDateOnly, ascending: true);

        Assert.Single(result);
        Assert.Equal("oldest", result[0].Details);
    }

    [Fact]
    public async Task Get_ToBoundary_ExcludesLaterEvents()
    {
        await using var db = await SeedLogsAsync();
        var service = new AuditLogService(db);

        var result = await service.GetAsync(null, T0, ascending: true);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, r => r.Details == "newest");
    }

    [Fact]
    public async Task Get_InvalidRange_Throws()
    {
        await using var db = await SeedLogsAsync();
        var service = new AuditLogService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAsync(T0.AddDays(2), T0, ascending: false));
    }

    [Fact]
    public async Task Get_ReturnsActorUsername_NotEntityGraph()
    {
        await using var db = await SeedLogsAsync();
        var service = new AuditLogService(db);

        var result = await service.GetAsync(null, null, ascending: false);

        Assert.All(result, r => Assert.Equal("admin", r.ActorUsername));
        Assert.All(result, r => Assert.Equal(DateTimeKind.Utc, r.CreatedAt.Kind));
    }

    private static async Task<AppDbContext> SeedLogsAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);

        db.Users.Add(new User
        {
            Id = 10,
            Username = "admin",
            Email = "admin@t.com",
            PasswordHash = "h"
        });
        await db.SaveChangesAsync();

        db.AuditLogs.AddRange(
            new AuditLog
            {
                ActorUserId = 10,
                Action = AuditLogActions.BotFlagged,
                TargetId = 1,
                Details = "oldest",
                CreatedAt = T0.AddDays(-1)
            },
            new AuditLog
            {
                ActorUserId = 10,
                Action = AuditLogActions.BotForceStopped,
                TargetId = 2,
                Details = "middle",
                CreatedAt = T0
            },
            new AuditLog
            {
                ActorUserId = 10,
                Action = AuditLogActions.BotFlagged,
                TargetId = 3,
                Details = "newest",
                CreatedAt = T0.AddDays(1)
            });
        await db.SaveChangesAsync();
        return db;
    }
}
