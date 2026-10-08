using System.Net;
using System.Net.Http.Json;
using CryptoTracker.API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CryptoTracker.API.Tests;

/// <summary>
/// Görev 88: kimlik doğrulama uçlarında rate limiting.
/// Her test kendi uygulama örneğini kullanır, böylece sayaçlar testler arasında paylaşılmaz.
/// Varsayılan limit (appsettings.json): IP başına dakikada 5 istek.
/// </summary>
public class RateLimitingIntegrationTests
{
    private const int AuthLimit = 5;

    [Fact]
    public async Task Login_WithinLimit_IsNotRejected()
    {
        using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < AuthLimit; i++)
        {
            var response = await client.PostAsJsonAsync("/api/Auth/login",
                new { usernameOrEmail = "yok", password = "yanlis" });

            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task Login_ExceedingLimit_Returns429_WithMessageAndRetryAfter()
    {
        using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < AuthLimit; i++)
            await client.PostAsJsonAsync("/api/Auth/login", new { usernameOrEmail = "yok", password = "yanlis" });

        var blocked = await client.PostAsJsonAsync("/api/Auth/login",
            new { usernameOrEmail = "yok", password = "yanlis" });

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.Contains("Retry-After"));
        var body = await blocked.Content.ReadAsStringAsync();
        Assert.Contains("Çok fazla deneme", body);
    }

    [Fact]
    public async Task ForgotPassword_ExceedingLimit_Returns429()
    {
        using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < AuthLimit; i++)
            await client.PostAsJsonAsync("/api/Auth/forgot-password", new { email = "biri@ornek.com" });

        var blocked = await client.PostAsJsonAsync("/api/Auth/forgot-password", new { email = "biri@ornek.com" });

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Register_ExceedingLimit_Returns429()
    {
        using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < AuthLimit; i++)
            await client.PostAsJsonAsync("/api/Auth/register", new { username = "", email = "", password = "" });

        var blocked = await client.PostAsJsonAsync("/api/Auth/register", new { username = "", email = "", password = "" });

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task AuthLimit_DoesNotAffectOtherEndpoints()
    {
        using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < AuthLimit + 1; i++)
            await client.PostAsJsonAsync("/api/Auth/login", new { usernameOrEmail = "yok", password = "yanlis" });

        // Login limiti dolsa bile başka bir uç 429 vermemeli (gevşek genel politika).
        var other = await client.GetAsync("/api/Auth/me");

        Assert.NotEqual(HttpStatusCode.TooManyRequests, other.StatusCode);
    }
}

public class RateLimitApiFactory : WebApplicationFactory<Program>
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
            // Arka plan servisleri (bot/alarm izleme) bu testlerde gereksiz.
            var backgroundServices = new[]
            {
                typeof(CryptoTracker.API.Services.BotMonitorService),
                typeof(CryptoTracker.API.Services.AlertMonitorService)
            };
            foreach (var descriptor in services.Where(d => backgroundServices.Contains(d.ImplementationType)).ToList())
                services.Remove(descriptor);
        });
    }
}
