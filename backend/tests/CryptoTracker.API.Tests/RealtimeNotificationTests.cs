using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Hubs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CryptoTracker.API.Tests;

/// <summary>
/// #102: SignalR ile anlık bildirimler. İş servisleri sahte IRealtimeNotifier ile, hub ise
/// WebApplicationFactory üzerinde gerçek JWT doğrulamasıyla test edilir.
/// </summary>
public class RealtimeNotificationTests
{
    private static readonly DateTime T0 = new(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);

    // ---------------- Alarm ----------------

    [Fact]
    public async Task AlertTriggered_IsSentOnlyToAlertOwner_WithSavedSignalId()
    {
        await using var db = CreateDb();
        var sefaAlert = CreateAlert(userId: 1, "BTCUSDT", target: 100m, AlertDirection.Above);
        var otherAlert = CreateAlert(userId: 2, "BTCUSDT", target: 200m, AlertDirection.Above);
        db.PriceAlerts.AddRange(sefaAlert, otherAlert);
        await db.SaveChangesAsync();

        var realtime = new RecordingRealtimeNotifier();
        var processor = new AlertMonitoringProcessor(
            NullLogger<AlertMonitoringProcessor>.Instance, new FixedClock(T0), realtime: realtime);

        await processor.ProcessAsync(db, PriceIs("BTCUSDT", 150m), CancellationToken.None);

        var sent = Assert.Single(realtime.Sent);
        Assert.Equal(1, sent.UserId);
        Assert.Equal(RealtimeEvents.AlertTriggered, sent.EventName);
        var payload = Assert.IsType<AlertTriggeredEvent>(sent.Payload);
        var saved = await db.AlertSignals.SingleAsync();
        Assert.Equal(saved.Id, payload.Id);
        Assert.Equal(sefaAlert.Id, payload.AlertId);
        Assert.Equal("BTCUSDT", payload.Symbol);
        Assert.Equal(150m, payload.PriceAtTrigger);
        Assert.Equal(T0, payload.TriggeredAt);
    }

    [Fact]
    public async Task AlertNotTriggered_SendsNothing()
    {
        await using var db = CreateDb();
        db.PriceAlerts.Add(CreateAlert(userId: 1, "BTCUSDT", target: 100m, AlertDirection.Above));
        await db.SaveChangesAsync();

        var realtime = new RecordingRealtimeNotifier();
        var processor = new AlertMonitoringProcessor(
            NullLogger<AlertMonitoringProcessor>.Instance, new FixedClock(T0), realtime: realtime);

        await processor.ProcessAsync(db, PriceIs("BTCUSDT", 90m), CancellationToken.None);

        Assert.Empty(realtime.Sent);
    }

    // ---------------- Bot ----------------

    [Fact]
    public async Task BotSignal_Executed_IsSentToBotOwner()
    {
        await using var db = CreateDb();
        var bot = await SeedBotAsync(db, userId: 7, balance: 10_000m);
        var realtime = new RecordingRealtimeNotifier();
        var executor = new BotAutoTradeExecutor(db, new PortfolioService(db), realtime);

        var signal = await executor.ExecuteAsync(bot, BotSignalType.Buy, 100m, 25m);

        var sent = Assert.Single(realtime.Sent);
        Assert.Equal(7, sent.UserId);
        Assert.Equal(RealtimeEvents.BotSignal, sent.EventName);
        var payload = Assert.IsType<BotSignalResponse>(sent.Payload);
        Assert.Equal(signal.Id, payload.Id);
        Assert.Equal(BotSignalStatus.Approved, payload.Status);
        Assert.Equal(BotSignalReason.Strategy, payload.Reason);
    }

    [Fact]
    public async Task BotSignal_Failed_IsAlsoSent_SoUserSeesTheError()
    {
        await using var db = CreateDb();
        var bot = await SeedBotAsync(db, userId: 7, balance: 0m);
        var realtime = new RecordingRealtimeNotifier();
        var executor = new BotAutoTradeExecutor(db, new PortfolioService(db), realtime);

        await executor.ExecuteAsync(bot, BotSignalType.Buy, 100m, 25m);

        var payload = Assert.IsType<BotSignalResponse>(Assert.Single(realtime.Sent).Payload);
        Assert.Equal(BotSignalStatus.Failed, payload.Status);
    }

    [Fact]
    public async Task PendingSignal_UsesPendingEvent()
    {
        var realtime = new RecordingRealtimeNotifier();
        var bot = new TradingBot { Id = 3, UserId = 5, Symbol = "ETHUSDT" };
        var signal = new BotSignal { Id = 11, BotId = 3, Status = BotSignalStatus.Pending };

        await realtime.SendBotSignalAsync(bot, signal);

        var sent = Assert.Single(realtime.Sent);
        Assert.Equal(5, sent.UserId);
        Assert.Equal(RealtimeEvents.BotSignalPending, sent.EventName);
    }

    // ---------------- Hub ve token ----------------

    [Fact]
    public async Task Hub_RejectsConnectionWithoutToken()
    {
        await using var factory = new RealtimeApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync($"{NotificationHub.Path}/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Hub_AcceptsTokenFromQueryString()
    {
        await using var factory = new RealtimeApiFactory();
        var (_, token) = await factory.SeedUserWithTokenAsync("sefa");
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            $"{NotificationHub.Path}/negotiate?negotiateVersion=1&access_token={token}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NormalApi_IgnoresTokenInQueryString_ButAcceptsHeader()
    {
        await using var factory = new RealtimeApiFactory();
        var (_, token) = await factory.SeedUserWithTokenAsync("sefa");
        var client = factory.CreateClient();

        var withQuery = await client.GetAsync($"/api/Portfolio/history?range=24h&access_token={token}");
        Assert.Equal(HttpStatusCode.Unauthorized, withQuery.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var withHeader = await client.GetAsync("/api/Portfolio/history?range=24h");
        Assert.Equal(HttpStatusCode.OK, withHeader.StatusCode);
    }

    [Fact]
    public async Task TwoConnectedUsers_EventReachesOnlyItsOwner()
    {
        await using var factory = new RealtimeApiFactory();
        var (sefaId, sefaToken) = await factory.SeedUserWithTokenAsync("sefa");
        var (_, aliToken) = await factory.SeedUserWithTokenAsync("ali");

        var sefaEvents = new ConcurrentQueue<AlertTriggeredEvent>();
        var aliEvents = new ConcurrentQueue<AlertTriggeredEvent>();
        var sefaReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var sefa = factory.CreateHubConnection(sefaToken);
        await using var ali = factory.CreateHubConnection(aliToken);
        sefa.On<AlertTriggeredEvent>(RealtimeEvents.AlertTriggered, e => { sefaEvents.Enqueue(e); sefaReceived.TrySetResult(); });
        ali.On<AlertTriggeredEvent>(RealtimeEvents.AlertTriggered, e => aliEvents.Enqueue(e));
        await sefa.StartAsync();
        await ali.StartAsync();

        var realtime = factory.Services.GetRequiredService<IRealtimeNotifier>();
        await realtime.SendToUserAsync(
            sefaId, RealtimeEvents.AlertTriggered, new AlertTriggeredEvent(42, 1, "BTCUSDT", 150m, T0));

        await sefaReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // Ali'ye yanlışlıkla gidecek bir mesaja da zaman tanı.
        await Task.Delay(500);

        Assert.Equal(42, Assert.Single(sefaEvents).Id);
        Assert.Empty(aliEvents);
    }

    // ---------------- Yardımcılar ----------------

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PriceAlert CreateAlert(int userId, string symbol, decimal target, AlertDirection direction) => new()
    {
        UserId = userId,
        Symbol = symbol,
        TargetPrice = target,
        Direction = direction,
        IsActive = true,
        Interval = AlertInterval.Minute,
        CreatedAt = T0
    };

    private static IBinancePriceService PriceIs(string symbol, decimal price)
    {
        var binance = new Mock<IBinancePriceService>();
        binance
            .Setup(b => b.GetPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal> { [symbol] = price });
        return binance.Object;
    }

    private static async Task<TradingBot> SeedBotAsync(AppDbContext db, int userId, decimal balance)
    {
        db.Users.Add(new User
        {
            Id = userId, Username = $"user{userId}", Email = $"u{userId}@test.com", PasswordHash = "h",
            VirtualBalance = balance
        });
        var bot = new TradingBot { UserId = userId, Symbol = "BTCUSDT", TradeQuantity = 1m };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();
        return bot;
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }
}

/// <summary>Gönderilen olayları listede tutar; hiçbir bağlantıya gerçekten gönderim yapmaz.</summary>
public sealed class RecordingRealtimeNotifier : IRealtimeNotifier
{
    public ConcurrentQueue<(int UserId, string EventName, object Payload)> Sent { get; } = new();

    public Task SendToUserAsync(int userId, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        Sent.Enqueue((userId, eventName, payload));
        return Task.CompletedTask;
    }
}

/// <summary>Gerçek JWT doğrulaması ve gerçek SignalR hub'ı ile çalışan test sunucusu.</summary>
public class RealtimeApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
        builder.ConfigureTestServices(services =>
        {
            var backgroundServices = new[] { typeof(BotMonitorService), typeof(AlertMonitorService) };
            foreach (var descriptor in services.Where(d => backgroundServices.Contains(d.ImplementationType)).ToList())
                services.Remove(descriptor);
        });
    }

    public async Task<(int UserId, string Token)> SeedUserWithTokenAsync(string username)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await RefreshTokenTests.SeedUserAsync(db, username, $"{username}@test.com");
        var token = scope.ServiceProvider.GetRequiredService<IJwtService>().GenerateToken(user, ["User"]);
        return (user.Id, token);
    }

    /// <summary>Test sunucusu gerçek soket açmadığı için bağlantı LongPolling ile test sunucusunun handler'ından geçer.</summary>
    public HubConnection CreateHubConnection(string token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, NotificationHub.Path), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
}
