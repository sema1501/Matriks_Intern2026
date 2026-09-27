using CryptoTracker.API.Data;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CryptoTracker.API.Tests;

/// <summary>
/// #82 — Görev 40 e-posta bildirimi (NotificationService) testleri.
///
/// Zaman mock'lanmıyor: servis "şimdi"yi kendisi okumuyor, triggeredAt parametresi alıyor.
/// Bu yüzden farklı triggeredAt değerleri geçirerek cooldown'ın içini ve dışını test ediyoruz.
/// Gerçek e-posta yerine gönderilenleri listeye yazan sahte bir IEmailSender kullanılıyor.
/// </summary>
public class NotificationServiceTests
{
    private static readonly DateTime T0 = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    // ---------------- Fiyat alarmı ----------------

    [Fact]
    public async Task PriceAlert_FirstTrigger_SendsEmail_AndStoresTimestamp()
    {
        var (db, sender, service) = Create();
        var alert = await SeedAlertAsync(db, await SeedUserAsync(db));

        await service.NotifyPriceAlertAsync(alert, 105m, T0);

        Assert.Single(sender.Sent);
        Assert.Equal("sefa@test.com", sender.Sent[0].To);
        Assert.Equal(T0, alert.LastEmailNotifiedAt);
    }

    [Fact]
    public async Task PriceAlert_WithinCooldown_DoesNotSendSecondEmail()
    {
        var (db, sender, service) = Create(cooldownMinutes: 60);
        var alert = await SeedAlertAsync(db, await SeedUserAsync(db));

        await service.NotifyPriceAlertAsync(alert, 105m, T0);
        await service.NotifyPriceAlertAsync(alert, 106m, T0.AddMinutes(59));

        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task PriceAlert_AfterCooldown_SendsAgain()
    {
        var (db, sender, service) = Create(cooldownMinutes: 60);
        var alert = await SeedAlertAsync(db, await SeedUserAsync(db));

        await service.NotifyPriceAlertAsync(alert, 105m, T0);
        await service.NotifyPriceAlertAsync(alert, 106m, T0.AddMinutes(60)); // sınır: tam 60 dk

        Assert.Equal(2, sender.Sent.Count);
    }

    [Fact]
    public async Task PriceAlert_NotificationsDisabled_NeverSends()
    {
        var (db, sender, service) = Create();
        var alert = await SeedAlertAsync(db, await SeedUserAsync(db, notificationsEnabled: false));

        await service.NotifyPriceAlertAsync(alert, 105m, T0);

        Assert.Empty(sender.Sent);
        Assert.Null(alert.LastEmailNotifiedAt);
    }

    [Fact]
    public async Task PriceAlert_EmptyEmail_SkipsSilently_WithoutThrowing()
    {
        var (db, sender, service) = Create();
        var alert = await SeedAlertAsync(db, await SeedUserAsync(db, email: ""));

        var ex = await Record.ExceptionAsync(() => service.NotifyPriceAlertAsync(alert, 105m, T0));

        Assert.Null(ex);
        Assert.Empty(sender.Sent);
    }

    // ---------------- Bot sinyali ----------------

    [Fact]
    public async Task BotSignal_FirstTrigger_SendsEmail_AndStoresTimestamp()
    {
        var (db, sender, service) = Create();
        var bot = await SeedBotAsync(db, await SeedUserAsync(db));

        await service.NotifyBotSignalAsync(bot, BotSignalType.Buy, 100m, 25m, T0);

        Assert.Single(sender.Sent);
        var stored = await db.TradingBots.AsNoTracking().SingleAsync();
        Assert.Equal(T0, stored.LastEmailNotifiedAt);
    }

    [Fact]
    public async Task BotSignal_WithinCooldown_DoesNotSendSecondEmail()
    {
        var (db, sender, service) = Create(cooldownMinutes: 60);
        var bot = await SeedBotAsync(db, await SeedUserAsync(db));

        await service.NotifyBotSignalAsync(bot, BotSignalType.Buy, 100m, 25m, T0);
        await service.NotifyBotSignalAsync(bot, BotSignalType.Sell, 110m, 75m, T0.AddMinutes(30));

        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task BotSignal_AfterCooldown_SendsAgain()
    {
        var (db, sender, service) = Create(cooldownMinutes: 60);
        var bot = await SeedBotAsync(db, await SeedUserAsync(db));

        await service.NotifyBotSignalAsync(bot, BotSignalType.Buy, 100m, 25m, T0);
        await service.NotifyBotSignalAsync(bot, BotSignalType.Sell, 110m, 75m, T0.AddMinutes(61));

        Assert.Equal(2, sender.Sent.Count);
    }

    [Fact]
    public async Task BotSignal_NotificationsDisabled_NeverSends()
    {
        var (db, sender, service) = Create();
        var bot = await SeedBotAsync(db, await SeedUserAsync(db, notificationsEnabled: false));

        await service.NotifyBotSignalAsync(bot, BotSignalType.Buy, 100m, 25m, T0);

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task BotSignal_EmptyEmail_SkipsSilently_WithoutThrowing()
    {
        var (db, sender, service) = Create();
        var bot = await SeedBotAsync(db, await SeedUserAsync(db, email: "   "));

        var ex = await Record.ExceptionAsync(() =>
            service.NotifyBotSignalAsync(bot, BotSignalType.Buy, 100m, 25m, T0));

        Assert.Null(ex);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task SenderFailure_IsSwallowed_SoMonitoringLoopKeepsRunning()
    {
        var (db, sender, service) = Create();
        sender.ThrowOnSend = true;
        var bot = await SeedBotAsync(db, await SeedUserAsync(db));

        var ex = await Record.ExceptionAsync(() =>
            service.NotifyBotSignalAsync(bot, BotSignalType.Buy, 100m, 25m, T0));

        Assert.Null(ex);
        // Gönderim başarısız olduğu için cooldown başlamamalı — bir sonraki tetiklenmede tekrar denenir.
        var stored = await db.TradingBots.AsNoTracking().SingleAsync();
        Assert.Null(stored.LastEmailNotifiedAt);
    }

    // ---------------- Yardımcılar ----------------

    private static (AppDbContext Db, FakeEmailSender Sender, NotificationService Service) Create(
        int cooldownMinutes = 60)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var sender = new FakeEmailSender();
        var service = new NotificationService(
            db,
            sender,
            Options.Create(new NotificationOptions { CooldownMinutes = cooldownMinutes }),
            NullLogger<NotificationService>.Instance);

        return (db, sender, service);
    }

    private static async Task<User> SeedUserAsync(
        AppDbContext db,
        bool notificationsEnabled = true,
        string email = "sefa@test.com")
    {
        var user = new User
        {
            Username = "sefa",
            Email = email,
            PasswordHash = "x",
            EmailNotificationsEnabled = notificationsEnabled,
            EmailConfirmed = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<PriceAlert> SeedAlertAsync(AppDbContext db, User user)
    {
        var alert = new PriceAlert
        {
            UserId = user.Id,
            Symbol = "BTCUSDT",
            TargetPrice = 100m,
            Direction = AlertDirection.Above
        };
        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync();
        return alert;
    }

    private static async Task<TradingBot> SeedBotAsync(AppDbContext db, User user)
    {
        var bot = new TradingBot
        {
            UserId = user.Id,
            Symbol = "BTCUSDT",
            TradeQuantity = 1m
        };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();
        return bot;
    }

    /// <summary>Gerçek e-posta göndermez; gönderilenleri listede tutar.</summary>
    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];
        public bool ThrowOnSend { get; set; }

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSend)
                throw new InvalidOperationException("SMTP down");

            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }
}
