using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoTracker.API.Tests;

public class AlertControllerIntegrationTests : IClassFixture<AlertApiFactory>
{
    private readonly AlertApiFactory _factory;

    public AlertControllerIntegrationTests(AlertApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSignals_returns_own_alert_signals()
    {
        var alertId = await SeedAlertAsync(ownerUserId: 1, withSignal: true);
        var client = CreateClientForUser(1);

        var response = await client.GetAsync($"/api/Alert/{alertId}/signals");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AlertSignalsResponse>();
        Assert.NotNull(body);
        Assert.Equal(alertId, body.AlertId);
        Assert.Equal(1, body.TotalCount);
    }

    [Fact]
    public async Task GetSignals_forbids_other_users_alert()
    {
        var alertId = await SeedAlertAsync(ownerUserId: 1, withSignal: true);
        var client = CreateClientForUser(2);

        var response = await client.GetAsync($"/api/Alert/{alertId}/signals");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Toggle_updates_own_alert()
    {
        var alertId = await SeedAlertAsync(ownerUserId: 1, withSignal: false);
        var client = CreateClientForUser(1);

        var response = await client.PatchAsJsonAsync($"/api/Alert/{alertId}/toggle", new ToggleAlertRequest(false));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AlertResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsActive);
    }

    [Fact]
    public async Task Toggle_forbids_other_users_alert()
    {
        var alertId = await SeedAlertAsync(ownerUserId: 1, withSignal: false);
        var client = CreateClientForUser(2);

        var response = await client.PatchAsJsonAsync($"/api/Alert/{alertId}/toggle", new ToggleAlertRequest(false));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClientForUser(int userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private async Task<int> SeedAlertAsync(int ownerUserId, bool withSignal)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var alert = new PriceAlert
        {
            UserId = ownerUserId,
            Symbol = "BTCUSDT",
            TargetPrice = 100m,
            Direction = AlertDirection.Above,
            IsActive = true,
            Interval = AlertInterval.Minute,
            CreatedAt = DateTime.UtcNow
        };
        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync();

        if (withSignal)
        {
            db.AlertSignals.Add(new AlertSignal
            {
                AlertId = alert.Id,
                PriceAtTrigger = 120m,
                TriggeredAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        return alert.Id;
    }
}

public class AlertApiFactory : WebApplicationFactory<Program>
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
            foreach (var descriptor in services.Where(d => d.ImplementationType == typeof(CryptoTracker.API.Services.AlertMonitorService)).ToList())
                services.Remove(descriptor);

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }
}

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["X-Test-UserId"].FirstOrDefault() ?? "1";
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, $"user{userId}")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
