using CryptoTracker.API.Data;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CryptoTracker.API.Tests;

/// <summary>
/// #100: RefreshToken tablosunun temizliği. Saat IClock ile ilerletilir.
/// </summary>
public class RefreshTokenCleanupTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Cleanup_DeletesExpiredToken()
    {
        await using var db = CreateDb();
        var expired = AddToken(db, expiresAt: Now.AddDays(-1));
        await db.SaveChangesAsync();

        var deleted = await CreateProcessor().CleanupAsync(db, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.Id == expired.Id));
    }

    [Fact]
    public async Task Cleanup_KeepsActiveToken()
    {
        await using var db = CreateDb();
        AddToken(db, expiresAt: Now.AddDays(3));
        await db.SaveChangesAsync();

        var deleted = await CreateProcessor().CleanupAsync(db, CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Single(await db.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task Cleanup_KeepsRecentlyRevokedToken_EvenIfExpired()
    {
        // Yeniden kullanım tespiti için iptal edilen token 30 gün tutulur; süresinin dolmuş olması önemli değil.
        await using var db = CreateDb();
        AddToken(db, expiresAt: Now.AddDays(-2), revokedAt: Now.AddDays(-10));
        AddToken(db, expiresAt: Now.AddDays(5), revokedAt: Now.AddMinutes(-1));
        await db.SaveChangesAsync();

        var deleted = await CreateProcessor().CleanupAsync(db, CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Equal(2, await db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Cleanup_DeletesTokenRevokedLongAgo()
    {
        await using var db = CreateDb();
        var old = AddToken(db, expiresAt: Now.AddDays(-25), revokedAt: Now.AddDays(-31));
        await db.SaveChangesAsync();

        var deleted = await CreateProcessor().CleanupAsync(db, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.Id == old.Id));
    }

    [Fact]
    public async Task Cleanup_AsClockMovesForward_RevokedTokenIsDeletedOnlyAfterRetention()
    {
        await using var db = CreateDb();
        AddToken(db, expiresAt: Now.AddDays(7), revokedAt: Now);
        await db.SaveChangesAsync();

        Assert.Equal(0, await CreateProcessor(Now.AddDays(29)).CleanupAsync(db, CancellationToken.None));
        Assert.Equal(1, await CreateProcessor(Now.AddDays(31)).CleanupAsync(db, CancellationToken.None));
        Assert.Empty(await db.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task Cleanup_RetentionIsReadFromOptions()
    {
        await using var db = CreateDb();
        AddToken(db, expiresAt: Now.AddDays(5), revokedAt: Now.AddDays(-3));
        await db.SaveChangesAsync();

        var processor = CreateProcessor(options: new RefreshTokenCleanupOptions { RevokedRetentionDays = 2 });

        Assert.Equal(1, await processor.CleanupAsync(db, CancellationToken.None));
    }

    [Fact]
    public void CleanupService_IsRegistered_AndReadsSettingsFromAppsettings()
    {
        using var factory = new RefreshTokenApiFactory();

        var hosted = factory.Services.GetServices<IHostedService>();
        Assert.Contains(hosted, s => s is RefreshTokenCleanupService);

        var options = factory.Services.GetRequiredService<IOptions<RefreshTokenCleanupOptions>>().Value;
        Assert.Equal(24, options.IntervalHours);
        Assert.Equal(30, options.RevokedRetentionDays);
    }

    private static RefreshToken AddToken(AppDbContext db, DateTime expiresAt, DateTime? revokedAt = null)
    {
        var token = new RefreshToken
        {
            UserId = 1,
            TokenHash = Guid.NewGuid().ToString("N"),
            CreatedAt = expiresAt.AddDays(-7),
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt
        };
        db.RefreshTokens.Add(token);
        return token;
    }

    private static RefreshTokenCleanupProcessor CreateProcessor(
        DateTime? now = null,
        RefreshTokenCleanupOptions? options = null) =>
        new(new FakeClock(now ?? Now),
            Options.Create(options ?? new RefreshTokenCleanupOptions()),
            NullLogger<RefreshTokenCleanupProcessor>.Instance);

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class FakeClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }
}
