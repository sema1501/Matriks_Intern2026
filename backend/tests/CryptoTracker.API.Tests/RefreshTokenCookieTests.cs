using System.Net;
using System.Net.Http.Json;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CryptoTracker.API.Tests;

/// <summary>#107: refresh token'ın HttpOnly cookie ile taşınması ve CSRF koruması.</summary>
public class RefreshTokenCookieTests
{
    private const string Password = "Sifre123!";
    private const string AllowedOrigin = "http://localhost:3000";

    // ---------------- Login ----------------

    [Fact]
    public async Task Login_SetsHttpOnlyCookie_AndBodyHasNoRefreshToken()
    {
        using var factory = await CreateFactoryAsync();
        var client = ManualCookieClient(factory);

        var response = await PostLoginAsync(client);

        response.EnsureSuccessStatusCode();
        var cookie = SetCookieHeader(response);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/Auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"max-age={TimeSpan.FromDays(7).TotalSeconds}", cookie, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"token\"", body);
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CookieValue(response), body);
    }

    [Fact]
    public async Task SecureFlag_IsOnByDefault()
    {
        using var factory = await CreateFactoryAsync(secureCookie: null);

        var response = await PostLoginAsync(ManualCookieClient(factory));

        Assert.Contains("secure", SetCookieHeader(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SecureFlag_CanBeTurnedOffByConfiguration()
    {
        using var factory = await CreateFactoryAsync(secureCookie: false);

        var response = await PostLoginAsync(ManualCookieClient(factory));

        Assert.DoesNotContain("secure", SetCookieHeader(response), StringComparison.OrdinalIgnoreCase);
    }

    // ---------------- Refresh ----------------

    [Fact]
    public async Task Refresh_WithCookie_ReturnsAccessTokenAndNewCookie()
    {
        using var factory = await CreateFactoryAsync();
        var client = ManualCookieClient(factory);
        var login = await PostLoginAsync(client);
        var oldToken = CookieValue(login);

        var refresh = await PostRefreshAsync(client, oldToken);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.NotEqual(oldToken, CookieValue(refresh));

        var raw = await refresh.Content.ReadAsStringAsync();
        Assert.DoesNotContain("refreshToken", raw, StringComparison.OrdinalIgnoreCase);

        var accessToken = (await refresh.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["token"].ToString();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/Auth/me");
        me.Headers.Authorization = new("Bearer", accessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(me)).StatusCode);
    }

    [Fact]
    public async Task Refresh_WorksWithRealCookieHandling()
    {
        // İstemci cookie'yi kendisi saklayıp gönderiyor: Path ve HttpOnly ayarları gerçek akışta da çalışıyor.
        using var factory = await CreateFactoryAsync();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        (await PostLoginAsync(client)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await PostRefreshAsync(client, cookie: null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostRefreshAsync(client, cookie: null)).StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        using var factory = await CreateFactoryAsync();

        var refresh = await PostRefreshAsync(ManualCookieClient(factory), cookie: null);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_TokenInBody_IsIgnored()
    {
        using var factory = await CreateFactoryAsync();
        var client = ManualCookieClient(factory);
        var login = await PostLoginAsync(client);

        var request = RefreshRequest(cookie: null);
        request.Content = JsonContent.Create(new { refreshToken = CookieValue(login) });
        var refresh = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReusedCookie_Returns401_AndRevokesRotatedOne()
    {
        // Tolerans penceresi kapalı: tekrar kullanım hemen saldırı sayılır.
        using var factory = await CreateFactoryAsync(reuseGraceSeconds: 0);
        var client = ManualCookieClient(factory);
        var oldToken = CookieValue(await PostLoginAsync(client));
        var rotated = CookieValue(await PostRefreshAsync(client, oldToken));

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostRefreshAsync(client, oldToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostRefreshAsync(client, rotated)).StatusCode);
    }

    [Fact]
    public async Task Refresh_SecondTabInsideGraceWindow_Gets401WithCode_AndSessionStaysOpen()
    {
        using var factory = await CreateFactoryAsync();
        var client = ManualCookieClient(factory);
        var oldToken = CookieValue(await PostLoginAsync(client));
        var rotated = CookieValue(await PostRefreshAsync(client, oldToken));

        var second = await PostRefreshAsync(client, oldToken);

        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal(RefreshTokenAlreadyRotatedException.ErrorCode, body!["code"]);
        // Reddedilen istek cookie'yi değiştirmez, ilk sekmenin aldığı yeni token geçerli kalır.
        Assert.False(second.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.OK, (await PostRefreshAsync(client, rotated)).StatusCode);
    }

    // ---------------- CSRF ----------------

    [Theory]
    [InlineData("/api/Auth/refresh")]
    [InlineData("/api/Auth/logout")]
    public async Task ForeignOrigin_Returns403(string path)
    {
        using var factory = await CreateFactoryAsync();
        var client = ManualCookieClient(factory);
        var token = CookieValue(await PostLoginAsync(client));

        var request = AuthRequest(path, token);
        request.Headers.Add("Origin", "https://kotu-site.example");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData("/api/Auth/refresh")]
    [InlineData("/api/Auth/logout")]
    public async Task MissingCustomHeader_Returns403(string path)
    {
        using var factory = await CreateFactoryAsync();
        var client = ManualCookieClient(factory);
        var token = CookieValue(await PostLoginAsync(client));

        var request = AuthRequest(path, token);
        request.Headers.Remove("X-Requested-With");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task AllowedOriginWithHeader_Works_AndRejectedRequestDoesNotUseToken()
    {
        using var factory = await CreateFactoryAsync();
        var client = ManualCookieClient(factory);
        var token = CookieValue(await PostLoginAsync(client));

        var foreign = AuthRequest("/api/Auth/refresh", token);
        foreign.Headers.Add("Origin", "https://kotu-site.example");
        await client.SendAsync(foreign);

        // Reddedilen istek token'ı döndürmedi, gerçek sayfanın isteği aynı token'la çalışıyor.
        var allowed = AuthRequest("/api/Auth/refresh", token);
        allowed.Headers.Add("Origin", AllowedOrigin);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(allowed)).StatusCode);
    }

    // ---------------- Logout ----------------

    [Fact]
    public async Task Logout_DeletesCookie_AndOldTokenStopsWorking()
    {
        using var factory = await CreateFactoryAsync();
        var client = ManualCookieClient(factory);
        var token = CookieValue(await PostLoginAsync(client));

        var logout = await client.SendAsync(AuthRequest("/api/Auth/logout", token));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var cookie = SetCookieHeader(logout);
        Assert.Contains("refreshToken=;", cookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/Auth", cookie, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostRefreshAsync(client, token)).StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutCookie_StillSucceeds()
    {
        using var factory = await CreateFactoryAsync();

        var logout = await ManualCookieClient(factory).SendAsync(AuthRequest("/api/Auth/logout", cookie: null));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    // ---------------- Yardımcılar ----------------

    private static async Task<RefreshTokenApiFactory> CreateFactoryAsync(int? reuseGraceSeconds = null, bool? secureCookie = false)
    {
        var factory = new RefreshTokenApiFactory(reuseGraceSeconds, secureCookie);
        await factory.SeedUserAsync();
        return factory;
    }

    // Cookie'ler istemcide saklanmaz: her testte hangi token'ın gittiği açıkça görünsün diye.
    private static HttpClient ManualCookieClient(RefreshTokenApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client)
        => client.PostAsJsonAsync("/api/Auth/login", new { usernameOrEmail = "sefa", password = Password });

    private static Task<HttpResponseMessage> PostRefreshAsync(HttpClient client, string? cookie)
        => client.SendAsync(RefreshRequest(cookie));

    private static HttpRequestMessage RefreshRequest(string? cookie) => AuthRequest("/api/Auth/refresh", cookie);

    private static HttpRequestMessage AuthRequest(string path, string? cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (cookie is not null)
            request.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={cookie}");
        return request;
    }

    private static string SetCookieHeader(HttpResponseMessage response)
        => Assert.Single(response.Headers.GetValues("Set-Cookie"));

    private static string CookieValue(HttpResponseMessage response)
        => SetCookieHeader(response).Split(';')[0].Split('=', 2)[1];
}
