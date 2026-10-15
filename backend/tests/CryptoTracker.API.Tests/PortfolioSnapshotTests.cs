using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CryptoTracker.API.Tests;

/// <summary>
/// Portföy değer geçmişi: snapshot hesabı, çift kayıt önleme, saklama politikası ve history ucu.
/// Zaman IClock ile sahte veriliyor; gerçek saat beklenmiyor.
/// </summary>
public class PortfolioSnapshotTests
{
    private static readonly DateTime T0 = new(2026, 10, 15, 10, 20, 0, DateTimeKind.Utc);

    // ── Snapshot hesabı ─────────────────────────────────────────

    [Fact]
    public void BuildSnapshot_CashPlusQuantityTimesLivePrice()
    {
        var holdings = new[]
        {
            Holding(1, "BTCUSDT", 0.5m, avg: 50_000m),
            Holding(1, "ETHUSDT", 2m, avg: 3_000m)
        };
        var prices = Prices(("BTCUSDT", 60_000m), ("ETHUSDT", 2_500m));

        var snapshot = PortfolioSnapshotProcessor.BuildSnapshot(1, 1_000m, holdings, prices, T0);

        Assert.Equal(1_000m, snapshot.CashBalance);
        Assert.Equal(35_000m, snapshot.HoldingsValue);
        Assert.Equal(36_000m, snapshot.TotalValue);
        Assert.Equal(T0, snapshot.TakenAt);
    }

    [Fact]
    public void BuildSnapshot_MissingPrice_FallsBackToAvgBuyPrice()
    {
        var holdings = new[] { Holding(1, "OLDCOIN", 10m, avg: 3m) };

        var snapshot = PortfolioSnapshotProcessor.BuildSnapshot(1, 100m, holdings, Prices(), T0);

        Assert.Equal(30m, snapshot.HoldingsValue);
        Assert.Equal(130m, snapshot.TotalValue);
    }

    [Fact]
    public async Task Process_CreatesOneSnapshotPerUser_WithSingleBinanceCall()
    {
        await using var db = CreateDb();
        SeedUser(db, 1, cash: 1_000m);
        SeedUser(db, 2, cash: 5_000m);
        SeedUser(db, 3, cash: 10_000m); // coini yok
        db.PortfolioHoldings.AddRange(
            Holding(1, "BTCUSDT", 1m, avg: 50_000m),
            Holding(2, "BTCUSDT", 0.1m, avg: 50_000m),
            Holding(2, "ETHUSDT", 1m, avg: 3_000m));
        await db.SaveChangesAsync();

        var binance = BinanceMock(("BTCUSDT", 60_000m), ("ETHUSDT", 2_000m));
        var result = await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(3, result.CreatedCount);
        Assert.Equal(2, result.SymbolCount);
        Assert.Equal(1, result.PriceRequestCount);
        binance.Verify(b => b.GetPricesAsync(
            It.Is<IEnumerable<string>>(s => s.OrderBy(x => x).SequenceEqual(new[] { "BTCUSDT", "ETHUSDT" })),
            It.IsAny<CancellationToken>()), Times.Once);

        var snapshots = await db.PortfolioSnapshots.AsNoTracking().OrderBy(s => s.UserId).ToListAsync();
        Assert.Equal(new[] { 61_000m, 13_000m, 10_000m }, snapshots.Select(s => s.TotalValue));
        Assert.All(snapshots, s => Assert.Equal(T0, s.TakenAt));
    }

    [Fact]
    public async Task Process_BinanceCallCount_DoesNotGrowWithUserCount()
    {
        await using var db = CreateDb();
        for (var id = 1; id <= 50; id++)
        {
            SeedUser(db, id, cash: 100m);
            db.PortfolioHoldings.Add(Holding(id, "BTCUSDT", 1m, avg: 1m));
        }
        await db.SaveChangesAsync();

        var binance = BinanceMock(("BTCUSDT", 2m));
        var result = await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(50, result.CreatedCount);
        binance.Verify(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_UserWithoutHoldings_DoesNotCallBinance()
    {
        await using var db = CreateDb();
        SeedUser(db, 1, cash: 10_000m);
        await db.SaveChangesAsync();

        var binance = new Mock<IBinancePriceService>(MockBehavior.Strict);
        var result = await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(0, result.PriceRequestCount);
        Assert.Equal(10_000m, (await db.PortfolioSnapshots.SingleAsync()).TotalValue);
    }

    // ── Çift kayıt önleme ───────────────────────────────────────

    [Fact]
    public async Task Process_SameHourTwice_DoesNotDuplicate()
    {
        await using var db = CreateDb();
        SeedUser(db, 1, cash: 1_000m);
        db.PortfolioHoldings.Add(Holding(1, "BTCUSDT", 1m, avg: 1m));
        await db.SaveChangesAsync();
        var binance = BinanceMock(("BTCUSDT", 10m));

        await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);
        // Uygulama aynı saat içinde yeniden başladı: 10:20 → 10:55
        var second = await CreateProcessor(T0.AddMinutes(35)).ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(1, second.SkippedUserCount);
        Assert.Single(await db.PortfolioSnapshots.ToListAsync());
        // Kaydı zaten olan kullanıcılar için fiyat da istenmez.
        binance.Verify(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_NextHour_CreatesNewSnapshot()
    {
        await using var db = CreateDb();
        SeedUser(db, 1, cash: 1_000m);
        await db.SaveChangesAsync();
        var binance = new Mock<IBinancePriceService>(MockBehavior.Strict);

        await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);
        await CreateProcessor(T0.AddMinutes(45)).ProcessAsync(db, binance.Object, CancellationToken.None); // 11:05

        Assert.Equal(2, await db.PortfolioSnapshots.CountAsync());
    }

    [Fact]
    public async Task Process_NewUserLaterInSameHour_GetsOwnSnapshot()
    {
        await using var db = CreateDb();
        SeedUser(db, 1, cash: 1_000m);
        await db.SaveChangesAsync();
        var binance = new Mock<IBinancePriceService>(MockBehavior.Strict);

        await CreateProcessor(T0).ProcessAsync(db, binance.Object, CancellationToken.None);
        SeedUser(db, 2, cash: 10_000m);
        await db.SaveChangesAsync();
        var second = await CreateProcessor(T0.AddMinutes(5)).ProcessAsync(db, binance.Object, CancellationToken.None);

        Assert.Equal(1, second.CreatedCount);
        Assert.Equal(new[] { 1, 2 }, await db.PortfolioSnapshots.OrderBy(s => s.UserId).Select(s => s.UserId).ToListAsync());
    }

    [Fact]
    public async Task Process_BinanceDown_SkipsCycleInsteadOfWritingZeroValues()
    {
        await using var db = CreateDb();
        SeedUser(db, 1, cash: 1_000m);
        db.PortfolioHoldings.Add(Holding(1, "BTCUSDT", 1m, avg: 50_000m));
        await db.SaveChangesAsync();

        var result = await CreateProcessor(T0).ProcessAsync(db, BinanceMock().Object, CancellationToken.None);

        Assert.Equal(0, result.CreatedCount);
        Assert.Empty(await db.PortfolioSnapshots.ToListAsync());
    }

    // ── Saklama politikası ──────────────────────────────────────

    [Fact]
    public async Task Compact_OlderThan30Days_KeepsLastSnapshotPerDay_NewerUntouched()
    {
        await using var db = CreateDb();
        SeedUser(db, 1, cash: 1m);
        SeedUser(db, 2, cash: 1m);

        // Her iki kullanıcı için 40 günlük saatlik kayıt.
        var start = T0.Date.AddDays(-40);
        foreach (var userId in new[] { 1, 2 })
        {
            for (var t = start; t < T0.Date; t = t.AddHours(1))
            {
                db.PortfolioSnapshots.Add(new PortfolioSnapshot
                {
                    UserId = userId, TakenAt = t, CashBalance = 1m, TotalValue = t.Hour
                });
            }
        }
        await db.SaveChangesAsync();

        var processor = CreateProcessor(T0);
        var deleted = await processor.CompactOldSnapshotsAsync(db, T0, CancellationToken.None);

        var cutoff = T0.Date.AddDays(-30);
        var rows = await db.PortfolioSnapshots.AsNoTracking().ToListAsync();

        // 10 eski gün × 23 silinen kayıt × 2 kullanıcı
        Assert.Equal(10 * 23 * 2, deleted);

        var old = rows.Where(s => s.TakenAt < cutoff).ToList();
        Assert.Equal(10 * 2, old.Count);
        Assert.All(old, s => Assert.Equal(23, s.TakenAt.Hour)); // günün son kaydı kaldı
        Assert.Equal(10, old.Count(s => s.UserId == 1));

        // Son 30 gün saatlik kalır.
        Assert.Equal(30 * 24 * 2, rows.Count(s => s.TakenAt >= cutoff));

        // İkinci çalıştırmada silinecek bir şey kalmaz.
        Assert.Equal(0, await processor.CompactOldSnapshotsAsync(db, T0, CancellationToken.None));
    }

    [Fact]
    public async Task Compact_OlderThanOneYear_IsDeleted_DailyRecordsWithinYearKept()
    {
        await using var db = CreateDb();
        SeedUser(db, 1, cash: 1m);
        db.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { UserId = 1, TakenAt = T0.Date.AddDays(-400).AddHours(23), TotalValue = 1m },
            new PortfolioSnapshot { UserId = 1, TakenAt = T0.Date.AddDays(-366).AddHours(23), TotalValue = 2m },
            new PortfolioSnapshot { UserId = 1, TakenAt = T0.Date.AddDays(-364).AddHours(23), TotalValue = 3m },
            new PortfolioSnapshot { UserId = 1, TakenAt = T0.Date.AddDays(-100).AddHours(23), TotalValue = 4m });
        await db.SaveChangesAsync();

        var deleted = await CreateProcessor(T0).CompactOldSnapshotsAsync(db, T0, CancellationToken.None);

        Assert.Equal(2, deleted);
        var remaining = await db.PortfolioSnapshots.OrderBy(s => s.TakenAt).Select(s => s.TotalValue).ToListAsync();
        Assert.Equal(new[] { 3m, 4m }, remaining);
    }

    [Fact]
    public async Task Process_RunsCompaction()
    {
        await using var db = CreateDb();
        SeedUser(db, 1, cash: 1m);
        var oldDay = T0.Date.AddDays(-45);
        db.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { UserId = 1, TakenAt = oldDay.AddHours(1), TotalValue = 1m },
            new PortfolioSnapshot { UserId = 1, TakenAt = oldDay.AddHours(5), TotalValue = 5m });
        await db.SaveChangesAsync();

        var result = await CreateProcessor(T0).ProcessAsync(
            db, new Mock<IBinancePriceService>(MockBehavior.Strict).Object, CancellationToken.None);

        Assert.Equal(1, result.CompactedCount);
        var remainingOld = await db.PortfolioSnapshots.SingleAsync(s => s.TakenAt < T0.Date);
        Assert.Equal(5m, remainingOld.TotalValue);
    }

    // ── History ─────────────────────────────────────────────────

    [Theory]
    [InlineData("24h", 24)]
    [InlineData("7d", 7 * 24)]
    [InlineData("30d", 30 * 24)]
    [InlineData("all", 40 * 24)]
    [InlineData("7D", 7 * 24)]
    public async Task History_FiltersByRange(string range, int expectedCount)
    {
        await using var db = CreateDb();
        SeedHourly(db, userId: 1, hours: 40 * 24);
        await db.SaveChangesAsync();

        var response = await CreateHistoryService(db).GetHistoryAsync(1, range);

        Assert.Equal(expectedCount, response.Points.Count);
        Assert.True(response.Points.SequenceEqual(response.Points.OrderBy(p => p.TakenAt)));
    }

    [Fact]
    public async Task History_DefaultsTo24h()
    {
        await using var db = CreateDb();
        SeedHourly(db, userId: 1, hours: 100);
        await db.SaveChangesAsync();

        var response = await CreateHistoryService(db).GetHistoryAsync(1, null);

        Assert.Equal("24h", response.Range);
        Assert.Equal(24, response.Points.Count);
    }

    [Fact]
    public async Task History_ReturnsOnlyOwnSnapshots()
    {
        await using var db = CreateDb();
        SeedHourly(db, userId: 1, hours: 10);
        SeedHourly(db, userId: 2, hours: 10, value: 99m);
        await db.SaveChangesAsync();

        var response = await CreateHistoryService(db).GetHistoryAsync(1, "all");

        Assert.Equal(10, response.Points.Count);
        Assert.DoesNotContain(response.Points, p => p.TotalValue == 99m);
    }

    [Fact]
    public async Task History_CalculatesChangeBetweenFirstAndLast()
    {
        await using var db = CreateDb();
        db.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { UserId = 1, TakenAt = T0.AddHours(-5), TotalValue = 10_000m },
            new PortfolioSnapshot { UserId = 1, TakenAt = T0.AddHours(-3), TotalValue = 9_000m },
            new PortfolioSnapshot { UserId = 1, TakenAt = T0.AddHours(-1), TotalValue = 10_500m });
        await db.SaveChangesAsync();

        var response = await CreateHistoryService(db).GetHistoryAsync(1, "24h");

        Assert.Equal(500m, response.ChangeUsd);
        Assert.Equal(5m, response.ChangePercent);
    }

    [Fact]
    public async Task History_EmptyOrSinglePoint_HasNoChange()
    {
        await using var db = CreateDb();
        var service = CreateHistoryService(db);

        var empty = await service.GetHistoryAsync(1, "7d");
        Assert.Empty(empty.Points);
        Assert.Null(empty.ChangeUsd);
        Assert.Null(empty.ChangePercent);

        db.PortfolioSnapshots.Add(new PortfolioSnapshot { UserId = 1, TakenAt = T0.AddHours(-1), TotalValue = 1m });
        await db.SaveChangesAsync();

        var single = await service.GetHistoryAsync(1, "7d");
        Assert.Single(single.Points);
        Assert.Null(single.ChangeUsd);
    }

    [Theory]
    [InlineData("1y")]
    [InlineData("abc")]
    [InlineData("-1")]
    public async Task History_InvalidRange_Throws(string range)
    {
        await using var db = CreateDb();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateHistoryService(db).GetHistoryAsync(1, range));
    }

    // ── Yardımcılar ─────────────────────────────────────────────

    private static void SeedHourly(AppDbContext db, int userId, int hours, decimal value = 10_000m)
    {
        // T0'dan geriye doğru, her saatin ortasında bir kayıt (sınırlara denk gelmesin diye).
        for (var i = 0; i < hours; i++)
        {
            db.PortfolioSnapshots.Add(new PortfolioSnapshot
            {
                UserId = userId,
                TakenAt = T0.AddMinutes(-30).AddHours(-i),
                CashBalance = value,
                TotalValue = value
            });
        }
    }

    private static PortfolioHolding Holding(int userId, string symbol, decimal qty, decimal avg) => new()
    {
        UserId = userId, Symbol = symbol, Quantity = qty, AvgBuyPrice = avg
    };

    private static IReadOnlyDictionary<string, decimal> Prices(params (string Symbol, decimal Price)[] items) =>
        items.ToDictionary(i => i.Symbol, i => i.Price, StringComparer.Ordinal);

    private static Mock<IBinancePriceService> BinanceMock(params (string Symbol, decimal Price)[] items)
    {
        var mock = new Mock<IBinancePriceService>();
        mock.Setup(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Prices(items));
        return mock;
    }

    private static void SeedUser(AppDbContext db, int id, decimal cash) =>
        db.Users.Add(new User
        {
            Id = id, Username = $"user{id}", Email = $"u{id}@test.com", PasswordHash = "h",
            VirtualBalance = cash
        });

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PortfolioSnapshotProcessor CreateProcessor(DateTime now) =>
        new(new FakeClock(now),
            Options.Create(new PortfolioSnapshotOptions()),
            NullLogger<PortfolioSnapshotProcessor>.Instance);

    private static PortfolioHistoryService CreateHistoryService(AppDbContext db) =>
        new(db, new FakeClock(T0));

    private sealed class FakeClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }
}

/// <summary>GET /api/Portfolio/history ucunun gerçek HTTP üzerinden sahiplik ve doğrulama testleri.</summary>
public class PortfolioHistoryIntegrationTests(AlertApiFactory factory) : IClassFixture<AlertApiFactory>
{
    [Fact]
    public async Task History_ReturnsOnlyCallersSnapshots()
    {
        var ownerId = await SeedSnapshotsAsync(value: 12_345m);
        var otherId = ownerId + 1000;
        await SeedSnapshotsAsync(value: 77_777m, userId: otherId);

        var response = await CreateClient(ownerId).GetAsync("/api/Portfolio/history?range=all");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PortfolioHistoryResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Points.Count);
        Assert.All(body.Points, p => Assert.Equal(12_345m, p.TotalValue));
    }

    [Fact]
    public async Task History_UserIdQueryParameter_IsIgnored()
    {
        var ownerId = await SeedSnapshotsAsync(value: 1m);
        var victimId = ownerId + 2000;
        await SeedSnapshotsAsync(value: 55_555m, userId: victimId);

        var response = await CreateClient(ownerId)
            .GetAsync($"/api/Portfolio/history?range=all&userId={victimId}");

        var body = await response.Content.ReadFromJsonAsync<PortfolioHistoryResponse>();
        Assert.NotNull(body);
        Assert.DoesNotContain(body.Points, p => p.TotalValue == 55_555m);
    }

    [Fact]
    public async Task History_InvalidRange_Returns400()
    {
        var response = await CreateClient(1).GetAsync("/api/Portfolio/history?range=1y");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static int _nextUserId = 50_000;

    private async Task<int> SeedSnapshotsAsync(decimal value, int? userId = null)
    {
        var id = userId ?? Interlocked.Increment(ref _nextUserId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PortfolioSnapshots.AddRange(
            new PortfolioSnapshot { UserId = id, TakenAt = DateTime.UtcNow.AddHours(-2), TotalValue = value },
            new PortfolioSnapshot { UserId = id, TakenAt = DateTime.UtcNow.AddHours(-1), TotalValue = value });
        await db.SaveChangesAsync();
        return id;
    }

    private HttpClient CreateClient(int userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }
}
