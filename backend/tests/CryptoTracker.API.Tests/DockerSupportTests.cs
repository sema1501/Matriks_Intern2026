using System.Net;
using System.Net.Http.Json;
using CryptoTracker.API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CryptoTracker.API.Tests;

/// <summary>#105: Docker'da kullanılan sağlık ucu ve nginx arkasında gerçek istemci IP'sinin okunması.</summary>
public class DockerSupportTests
{
    private const int AuthLimit = 5;

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_IsNotRateLimited()
    {
        using var factory = new TightGeneralLimitFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task ForwardedHeadersEnabled_ClientsBehindProxy_HaveSeparateLimits()
    {
        using var factory = new ForwardedHeadersFactory(enabled: true);
        var client = factory.CreateClient();

        for (var i = 0; i < AuthLimit; i++)
            await LoginFrom(client, "203.0.113.10");

        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginFrom(client, "203.0.113.10")).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await LoginFrom(client, "203.0.113.20")).StatusCode);
    }

    [Fact]
    public async Task ForwardedHeadersDisabled_HeaderCannotBypassLimit()
    {
        using var factory = new ForwardedHeadersFactory(enabled: false);
        var client = factory.CreateClient();

        for (var i = 0; i < AuthLimit; i++)
            await LoginFrom(client, $"203.0.113.{i}");

        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginFrom(client, "203.0.113.99")).StatusCode);
    }

    [Fact]
    public async Task SeededAdmin_CanLoginOnFreshDatabase()
    {
        using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
            await DataSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        var response = await client.PostAsJsonAsync("/api/Auth/login",
            new { usernameOrEmail = "admin", password = "Admin123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<HttpResponseMessage> LoginFrom(HttpClient client, string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/login")
        {
            Content = JsonContent.Create(new { usernameOrEmail = "yok", password = "yanlis" })
        };
        request.Headers.Add("X-Forwarded-For", ip);
        return client.SendAsync(request);
    }

    private sealed class ForwardedHeadersFactory(bool enabled) : RateLimitApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("ForwardedHeaders:Enabled", enabled.ToString());
        }
    }

    private sealed class TightGeneralLimitFactory : RateLimitApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RateLimiting:GeneralPermitLimit", "1");
        }
    }
}
