using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CryptoTracker.API.Tests;

public class BotServiceTests
{
    private const int User1 = 1;
    private const int User2 = 2;

    // ── GET bots ────────────────────────────────────────────────

    [Fact]
    public async Task GetBots_ReturnsOnlyOwnBots()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        db.TradingBots.AddRange(
            new TradingBot { UserId = User1, Symbol = "BTCUSDT", TradeQuantity = 1m },
            new TradingBot { UserId = User2, Symbol = "ETHUSDT", TradeQuantity = 2m });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var bots = await service.GetBotsByUserAsync(User1);

        Assert.Single(bots);
        Assert.Equal("BTCUSDT", bots[0].Symbol);
    }

    // ── POST create ─────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_CreatesBotForUser()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var service = CreateService(db);

        var bot = await service.CreateBotAsync(User1,
            new CreateBotRequest("btcusdt", 25m, 75m, 0.5m));

        Assert.Equal("BTCUSDT", bot.Symbol);
        Assert.Equal(User1, (await db.TradingBots.SingleAsync()).UserId);
    }

    [Fact]
    public async Task Create_EmptySymbol_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateBotAsync(User1, new CreateBotRequest("", 30m, 70m, 1m)));
    }

    [Fact]
    public async Task Create_NonPositiveQuantity_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateBotAsync(User1, new CreateBotRequest("BTC", 30m, 70m, 0m)));
    }

    [Fact]
    public async Task Create_BuyRsiOutOfRange_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateBotAsync(User1, new CreateBotRequest("BTC", 101m, 70m, 1m)));
    }

    [Fact]
    public async Task Create_SellRsiOutOfRange_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateBotAsync(User1, new CreateBotRequest("BTC", 30m, -1m, 1m)));
    }

    [Fact]
    public async Task Create_BuyRsiGreaterOrEqualSellRsi_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateBotAsync(User1, new CreateBotRequest("BTC", 70m, 70m, 1m)));
    }

    [Fact]
    public async Task Create_DuplicateSymbol_Throws()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        db.TradingBots.Add(new TradingBot { UserId = User1, Symbol = "BTCUSDT", TradeQuantity = 1m });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateBotAsync(User1, new CreateBotRequest(" btcusdt ", 30m, 70m, 1m)));
    }

    // ── PATCH toggle ────────────────────────────────────────────

    [Fact]
    public async Task Toggle_OwnBot_TogglesIsActive()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = new TradingBot { UserId = User1, Symbol = "BTCUSDT", TradeQuantity = 1m, IsActive = true };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ToggleBotAsync(User1, bot.Id);

        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task Toggle_OtherUsersBot_ThrowsNotFound()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = new TradingBot { UserId = User1, Symbol = "BTCUSDT", TradeQuantity = 1m };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ToggleBotAsync(User2, bot.Id));
    }

    // ── GET signals ─────────────────────────────────────────────

    [Fact]
    public async Task GetSignals_OwnBot_ReturnsSignals()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = new TradingBot { UserId = User1, Symbol = "BTCUSDT", TradeQuantity = 1m };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();
        db.BotSignals.Add(new BotSignal
        {
            BotId = bot.Id, SignalType = BotSignalType.Buy,
            RsiValueAtSignal = 25m, PriceAtSignal = 50000m
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var signals = await service.GetSignalsAsync(User1, bot.Id);

        Assert.Single(signals);
    }

    [Fact]
    public async Task GetSignals_OtherUsersBot_ThrowsNotFound()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = new TradingBot { UserId = User1, Symbol = "BTCUSDT", TradeQuantity = 1m };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetSignalsAsync(User2, bot.Id));
    }

    // ── Approve Buy ─────────────────────────────────────────────

    [Fact]
    public async Task Approve_BuySignal_UpdatesBalanceAndHoldings()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);

        var service = CreateService(db);
        var result = await service.ApproveSignalAsync(User1, signal.Id);

        Assert.Equal(BotSignalStatus.Approved, result.Status);
        Assert.NotNull(result.Transaction);

        var user = await db.Users.FirstAsync(u => u.Id == User1);
        Assert.Equal(10_000m - 100m, user.VirtualBalance);

        var holding = await db.PortfolioHoldings.FirstAsync(h => h.UserId == User1);
        Assert.Equal(1m, holding.Quantity);

        var tx = await db.Transactions.FirstAsync(t => t.UserId == User1);
        Assert.Equal(TransactionType.Buy, tx.Type);
    }

    // ── Approve Sell ────────────────────────────────────────────

    [Fact]
    public async Task Approve_SellSignal_UpdatesBalanceAndHoldings()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        db.PortfolioHoldings.Add(new PortfolioHolding
        {
            UserId = User1, Symbol = "BTCUSDT", Quantity = 5m, AvgBuyPrice = 90m
        });
        await db.SaveChangesAsync();
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Sell, 100m, 1m);

        var service = CreateService(db);
        var result = await service.ApproveSignalAsync(User1, signal.Id);

        Assert.Equal(BotSignalStatus.Approved, result.Status);

        var user = await db.Users.FirstAsync(u => u.Id == User1);
        Assert.Equal(10_000m + 100m, user.VirtualBalance);
    }

    // ── Insufficient balance ────────────────────────────────────

    [Fact]
    public async Task Approve_InsufficientBalance_DoesNotMarkApproved()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var user = await db.Users.FirstAsync(u => u.Id == User1);
        user.VirtualBalance = 0m;
        await db.SaveChangesAsync();

        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);

        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveSignalAsync(User1, signal.Id));

        var updatedSignal = await db.BotSignals.AsNoTracking().FirstAsync(s => s.Id == signal.Id);
        Assert.Equal(BotSignalStatus.Pending, updatedSignal.Status);
    }

    // ── Insufficient holdings ───────────────────────────────────

    [Fact]
    public async Task Approve_InsufficientHoldings_DoesNotMarkApproved()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Sell, 100m, 1m);

        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveSignalAsync(User1, signal.Id));

        var updatedSignal = await db.BotSignals.AsNoTracking().FirstAsync(s => s.Id == signal.Id);
        Assert.Equal(BotSignalStatus.Pending, updatedSignal.Status);
    }

    // ── Reject ──────────────────────────────────────────────────

    [Fact]
    public async Task Reject_PendingSignal_SetsRejectedStatus()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);

        var service = CreateService(db);
        var result = await service.RejectSignalAsync(User1, signal.Id);

        Assert.Equal(BotSignalStatus.Rejected, result.Status);
        Assert.Null(result.Transaction);
    }

    [Fact]
    public async Task Reject_DoesNotAffectPortfolio()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);

        var service = CreateService(db);
        await service.RejectSignalAsync(User1, signal.Id);

        var user = await db.Users.FirstAsync(u => u.Id == User1);
        Assert.Equal(10_000m, user.VirtualBalance);
        Assert.Empty(await db.PortfolioHoldings.ToListAsync());
        Assert.Empty(await db.Transactions.ToListAsync());
    }

    // ── Expired signal ──────────────────────────────────────────

    [Fact]
    public async Task Approve_ExpiredSignal_SetsExpiredAndThrows()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);
        signal.CreatedAt = DateTime.UtcNow.AddMinutes(-20);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveSignalAsync(User1, signal.Id));

        var updated = await db.BotSignals.AsNoTracking().FirstAsync(s => s.Id == signal.Id);
        Assert.Equal(BotSignalStatus.Expired, updated.Status);
    }

    [Fact]
    public async Task Reject_ExpiredSignal_SetsExpiredAndThrows()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);
        signal.CreatedAt = DateTime.UtcNow.AddMinutes(-20);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejectSignalAsync(User1, signal.Id));

        var updated = await db.BotSignals.AsNoTracking().FirstAsync(s => s.Id == signal.Id);
        Assert.Equal(BotSignalStatus.Expired, updated.Status);
    }

    // ── Already processed signals ───────────────────────────────

    [Fact]
    public async Task Approve_AlreadyApproved_Throws()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);
        signal.Status = BotSignalStatus.Approved;
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveSignalAsync(User1, signal.Id));
    }

    [Fact]
    public async Task Approve_RejectedSignal_Throws()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);
        signal.Status = BotSignalStatus.Rejected;
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveSignalAsync(User1, signal.Id));
    }

    // ── Ownership on signals ────────────────────────────────────

    [Fact]
    public async Task Approve_OtherUsersSignal_ThrowsNotFound()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);

        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ApproveSignalAsync(User2, signal.Id));
    }

    [Fact]
    public async Task Reject_OtherUsersSignal_ThrowsNotFound()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);

        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.RejectSignalAsync(User2, signal.Id));
    }

    // ── Double approval / concurrency ───────────────────────────

    [Fact]
    public async Task Approve_SecondApprovalAfterFirst_CreatesNoSecondTransaction()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);

        var service = CreateService(db);
        await service.ApproveSignalAsync(User1, signal.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveSignalAsync(User1, signal.Id));

        Assert.Equal(1, await db.Transactions.CountAsync());

        var user = await db.Users.FirstAsync(u => u.Id == User1);
        Assert.Equal(10_000m - 100m, user.VirtualBalance);
    }

    [Fact]
    public async Task Approve_SimulatedConcurrentClaims_OnlyOneSucceeds()
    {
        // With InMemory, true concurrency is not testable.
        // This test simulates sequential "competing" claims against the same signal
        // to verify that the second attempt gets rejected by the status check.
        var dbName = Guid.NewGuid().ToString();

        await using (var db = CreateDb(dbName))
        {
            await SeedUsersAsync(db);
            await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);
        }

        // First claim succeeds
        await using (var db1 = CreateDb(dbName))
        {
            var service1 = CreateService(db1);
            var result = await service1.ApproveSignalAsync(User1, 1);
            Assert.Equal(BotSignalStatus.Approved, result.Status);
        }

        // Second claim fails
        await using (var db2 = CreateDb(dbName))
        {
            var service2 = CreateService(db2);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service2.ApproveSignalAsync(User1, 1));
        }

        // Only one transaction exists
        await using (var db3 = CreateDb(dbName))
        {
            Assert.Equal(1, await db3.Transactions.CountAsync());
        }
    }

    [Fact]
    public async Task ApproveAndReject_CompetingOnSameSignal_OnlyOneWins()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);

        var service = CreateService(db);
        await service.RejectSignalAsync(User1, signal.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveSignalAsync(User1, signal.Id));

        var updated = await db.BotSignals.AsNoTracking().FirstAsync(s => s.Id == signal.Id);
        Assert.Equal(BotSignalStatus.Rejected, updated.Status);
        Assert.Empty(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Expiration_CannotOverwriteApproved()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = new TradingBot { UserId = User1, Symbol = "BTCUSDT", TradeQuantity = 1m };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();

        var signal = new BotSignal
        {
            BotId = bot.Id, SignalType = BotSignalType.Buy,
            RsiValueAtSignal = 25m, PriceAtSignal = 100m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            Status = BotSignalStatus.Approved
        };
        db.BotSignals.Add(signal);
        await db.SaveChangesAsync();

        // GetSignals expires stale Pending signals — should not touch Approved
        var service = CreateService(db);
        var signals = await service.GetSignalsAsync(User1, bot.Id);

        Assert.Single(signals);
        Assert.Equal(BotSignalStatus.Approved, signals[0].Status);
    }

    [Fact]
    public async Task Expiration_CannotOverwriteRejected()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var bot = new TradingBot { UserId = User1, Symbol = "BTCUSDT", TradeQuantity = 1m };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();

        var signal = new BotSignal
        {
            BotId = bot.Id, SignalType = BotSignalType.Buy,
            RsiValueAtSignal = 25m, PriceAtSignal = 100m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            Status = BotSignalStatus.Rejected
        };
        db.BotSignals.Add(signal);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var signals = await service.GetSignalsAsync(User1, bot.Id);

        Assert.Single(signals);
        Assert.Equal(BotSignalStatus.Rejected, signals[0].Status);
    }

    [Fact]
    public async Task Approve_ExpiredPendingSignal_CannotBeClaimed()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);
        signal.CreatedAt = DateTime.UtcNow.AddMinutes(-16);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveSignalAsync(User1, signal.Id));

        Assert.Contains("süresi dolmuş", ex.Message);
        Assert.Empty(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Approve_ForeignSignal_CannotBeClaimedByAtomicUpdate()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var (_, signal) = await SeedBotWithSignalAsync(db, User1, BotSignalType.Buy, 100m, 1m);

        var service = CreateService(db);

        // User2 cannot claim User1's signal
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ApproveSignalAsync(User2, signal.Id));

        var updated = await db.BotSignals.AsNoTracking().FirstAsync(s => s.Id == signal.Id);
        Assert.Equal(BotSignalStatus.Pending, updated.Status);
    }

    // ── Helpers ─────────────────────────────────────────────────

    private static AppDbContext CreateDb(string? name = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedUsersAsync(AppDbContext db)
    {
        db.Users.AddRange(
            new User { Id = User1, Username = "user1", Email = "u1@test.com", PasswordHash = "h" },
            new User { Id = User2, Username = "user2", Email = "u2@test.com", PasswordHash = "h" });
        await db.SaveChangesAsync();
    }

    private static async Task<(TradingBot Bot, BotSignal Signal)> SeedBotWithSignalAsync(
        AppDbContext db, int userId, BotSignalType signalType, decimal price, decimal quantity)
    {
        var bot = new TradingBot
        {
            UserId = userId, Symbol = "BTCUSDT", TradeQuantity = quantity,
            BuyRsiThreshold = 30m, SellRsiThreshold = 70m
        };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();

        var signal = new BotSignal
        {
            BotId = bot.Id, SignalType = signalType,
            RsiValueAtSignal = 25m, PriceAtSignal = price,
            CreatedAt = DateTime.UtcNow, Status = BotSignalStatus.Pending
        };
        db.BotSignals.Add(signal);
        await db.SaveChangesAsync();

        return (bot, signal);
    }

    private static BotService CreateService(AppDbContext db)
    {
        var portfolioService = new PortfolioService(db);
        var opts = Options.Create(new TradingBotOptions { SignalExpirationMinutes = 15 });
        return new BotService(db, portfolioService, opts);
    }
}
