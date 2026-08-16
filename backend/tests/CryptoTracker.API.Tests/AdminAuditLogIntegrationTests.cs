using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoTracker.API.Tests;

public class AdminAuditLogIntegrationTests : IClassFixture<AdminApiFactory>
{
    private readonly AdminApiFactory _factory;

    public AdminAuditLogIntegrationTests(AdminApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuditLog_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/Admin/audit-log");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuditLog_NormalUser_Returns403()
    {
        var client = CreateClient(userId: 2, role: "User");
        var response = await client.GetAsync("/api/Admin/audit-log");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuditLog_Admin_Returns200()
    {
        var client = CreateClient(userId: 10, role: "Admin", username: "admin");
        var response = await client.GetAsync("/api/Admin/audit-log");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuditLog_SuperAdmin_Returns200()
    {
        var client = CreateClient(userId: 11, role: "SuperAdmin", username: "super");
        var response = await client.GetAsync("/api/Admin/audit-log");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Kill_AsAdmin_StopsBot_AndCreatesBotForceStoppedAudit()
    {
        var botId = await SeedBotAsync(ownerUserId: 1, symbol: "BTCUSDT");
        var client = CreateClient(userId: 10, role: "Admin", username: "admin");

        var response = await client.PostAsync($"/api/Admin/bots/{botId}/kill", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bot = await db.TradingBots.AsNoTracking().SingleAsync(b => b.Id == botId);
        Assert.False(bot.IsActive);

        var log = await db.AuditLogs.SingleAsync(a => a.TargetId == botId && a.Action == AuditLogActions.BotForceStopped);
        Assert.Equal(10, log.ActorUserId);
        Assert.NotEqual(1, log.ActorUserId);
        Assert.Equal(botId, log.TargetId);
        Assert.Contains(bot.Symbol, log.Details);
        Assert.DoesNotContain("password", log.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", log.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Kill_AlreadyStopped_PreservesWeek8Response_AndDoesNotCreateAudit()
    {
        var botId = await SeedBotAsync(ownerUserId: 1, symbol: "ADAUSDT", isActive: false);
        var auditCountBefore = await CountAuditsAsync();
        var client = CreateClient(userId: 10, role: "Admin", username: "admin");

        var response = await client.PostAsync($"/api/Admin/bots/{botId}/kill", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(botId, payload.GetProperty("botId").GetInt32());
        Assert.False(payload.GetProperty("isActive").GetBoolean());
        Assert.Contains("zaten durdurulmuş", payload.GetProperty("message").GetString());
        Assert.Equal(auditCountBefore, await CountAuditsAsync());
    }

    [Fact]
    public async Task Flag_AsAdmin_CreatesBotFlaggedAudit()
    {
        var botId = await SeedBotAsync(ownerUserId: 1, symbol: "ETHUSDT");
        var client = CreateClient(userId: 11, role: "SuperAdmin", username: "super");

        var response = await client.PatchAsJsonAsync(
            $"/api/Admin/Bot/{botId}/flag",
            new AdminBotActionRequest("Şüpheli aktivite"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bot = await db.TradingBots.AsNoTracking().SingleAsync(b => b.Id == botId);
        Assert.True(bot.IsFlagged);

        var log = await db.AuditLogs.SingleAsync(a => a.TargetId == botId && a.Action == AuditLogActions.BotFlagged);
        Assert.Equal(11, log.ActorUserId);
        Assert.Equal(botId, log.TargetId);
        Assert.Contains(bot.Symbol, log.Details);
        Assert.Contains("Şüpheli aktivite", log.Details);
        Assert.DoesNotContain("password", log.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", log.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Kill_MissingBot_DoesNotCreateAudit()
    {
        var auditCountBefore = await CountAuditsAsync();
        var client = CreateClient(userId: 10, role: "Admin", username: "admin");

        var response = await client.PostAsync("/api/Admin/bots/99999/kill", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(auditCountBefore, await CountAuditsAsync());
    }

    [Fact]
    public async Task Flag_MissingBot_DoesNotCreateAudit()
    {
        var auditCountBefore = await CountAuditsAsync();
        var client = CreateClient(userId: 10, role: "Admin", username: "admin");

        var response = await client.PatchAsJsonAsync(
            "/api/Admin/Bot/99999/flag",
            new AdminBotActionRequest("ghost"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(auditCountBefore, await CountAuditsAsync());
    }

    [Fact]
    public async Task AuditLog_SupportsDateFilterAndDefaultNewestFirst()
    {
        var older = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);
        await SeedAuditAsync(older, "old-event", targetId: 101);
        await SeedAuditAsync(newer, "new-event", targetId: 102);

        var client = CreateClient(userId: 10, role: "Admin", username: "admin");

        var all = await client.GetFromJsonAsync<List<AuditLogResponse>>("/api/Admin/audit-log");
        Assert.NotNull(all);
        Assert.True(all.Count >= 2);
        Assert.True(all[0].CreatedAt >= all[^1].CreatedAt);

        var filtered = await client.GetFromJsonAsync<List<AuditLogResponse>>(
            "/api/Admin/audit-log?from=2026-08-10T00:00:00Z&to=2026-08-20T00:00:00Z");
        Assert.NotNull(filtered);
        Assert.Contains(filtered, r => r.Details == "new-event");
        Assert.DoesNotContain(filtered, r => r.Details == "old-event");

        var invalid = await client.GetAsync("/api/Admin/audit-log?from=2026-08-20&to=2026-08-01");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Kill_NormalUser_Returns403_AndDoesNotAudit()
    {
        var botId = await SeedBotAsync(ownerUserId: 1, symbol: "XRPUSDT");
        var auditCountBefore = await CountAuditsAsync();
        var client = CreateClient(userId: 2, role: "User");

        var response = await client.PostAsync($"/api/Admin/bots/{botId}/kill", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(auditCountBefore, await CountAuditsAsync());
    }

    [Fact]
    public async Task Flag_NormalUser_Returns403_AndDoesNotAudit()
    {
        var botId = await SeedBotAsync(ownerUserId: 1, symbol: "SOLUSDT");
        var auditCountBefore = await CountAuditsAsync();
        var client = CreateClient(userId: 2, role: "User");

        var response = await client.PatchAsJsonAsync(
            $"/api/Admin/Bot/{botId}/flag",
            new AdminBotActionRequest("nope"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(auditCountBefore, await CountAuditsAsync());
    }

    private HttpClient CreateClient(int userId, string role, string? username = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("AdminTest");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-Username", username ?? $"user{userId}");
        return client;
    }

    private async Task<int> SeedBotAsync(int ownerUserId, string symbol, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureUsersAsync(db);

        var bot = new TradingBot
        {
            UserId = ownerUserId,
            Symbol = symbol + Guid.NewGuid().ToString("N")[..6],
            TradeQuantity = 1m,
            IsActive = isActive
        };
        db.TradingBots.Add(bot);
        await db.SaveChangesAsync();
        return bot.Id;
    }

    private async Task SeedAuditAsync(DateTime createdAt, string details, int targetId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureUsersAsync(db);
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = 10,
            Action = AuditLogActions.BotForceStopped,
            TargetId = targetId,
            Details = details,
            CreatedAt = createdAt
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> CountAuditsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.AuditLogs.CountAsync();
    }

    private static async Task EnsureUsersAsync(AppDbContext db)
    {
        if (!await db.Users.AnyAsync())
        {
            db.Users.AddRange(
                new User { Id = 1, Username = "owner", Email = "owner@t.com", PasswordHash = "h" },
                new User { Id = 2, Username = "user", Email = "user@t.com", PasswordHash = "h" },
                new User { Id = 10, Username = "admin", Email = "admin@t.com", PasswordHash = "h" },
                new User { Id = 11, Username = "super", Email = "super@t.com", PasswordHash = "h" });
            await db.SaveChangesAsync();
        }
    }
}

public class AdminApiFactory : WebApplicationFactory<Program>
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
            foreach (var descriptor in services
                         .Where(d => d.ImplementationType == typeof(BotMonitorService) ||
                                     d.ImplementationType == typeof(AlertMonitorService))
                         .ToList())
            {
                services.Remove(descriptor);
            }

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "AdminTest";
                    options.DefaultChallengeScheme = "AdminTest";
                })
                .AddScheme<AuthenticationSchemeOptions, AdminRoleAuthHandler>("AdminTest", _ => { });
        });
    }
}

public class AdminRoleAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = Request.Headers["X-Test-UserId"].FirstOrDefault() ?? "1";
        var role = Request.Headers["X-Test-Role"].FirstOrDefault() ?? "User";
        var username = Request.Headers["X-Test-Username"].FirstOrDefault() ?? $"user{userId}";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "AdminTest");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "AdminTest");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
