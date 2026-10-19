using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoTracker.API.Tests;

/// <summary>
/// #94: kısa ömürlü access token + refresh token (rotation, yeniden kullanım tespiti, iptaller).
/// </summary>
public class RefreshTokenTests
{
    private const string Password = "Sifre123!";

    // ---------------- Login ----------------

    [Fact]
    public async Task Login_ReturnsRefreshToken_AndStoresOnlyItsHash()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateAuthService(db);

        var response = await service.LoginAsync(new LoginRequest("sefa", Password));

        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        var stored = await db.RefreshTokens.SingleAsync();
        Assert.NotEqual(response.RefreshToken, stored.TokenHash);
        Assert.Equal(JwtService.HashRefreshToken(response.RefreshToken), stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.Null(stored.RevokedAt);
        Assert.True(stored.ExpiresAt > DateTime.UtcNow.AddDays(6));
    }

    [Fact]
    public void AccessToken_LifetimeIsReadFromConfiguration()
    {
        var jwt = new JwtService(CreateConfig(accessTokenMinutes: 1));

        var token = jwt.GenerateToken(new User { Id = 1, Username = "sefa", Email = "sefa@test.com" }, ["User"]);
        var expires = new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;

        Assert.InRange(expires, DateTime.UtcNow.AddSeconds(30), DateTime.UtcNow.AddMinutes(1).AddSeconds(5));
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsDifferentValueEachTime()
    {
        var jwt = new JwtService(CreateConfig());

        Assert.NotEqual(jwt.GenerateRefreshToken(), jwt.GenerateRefreshToken());
    }

    // ---------------- Refresh / rotation ----------------

    [Fact]
    public async Task Refresh_ValidToken_RotatesAndRevokesOldOne()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateAuthService(db);
        var login = await service.LoginAsync(new LoginRequest("sefa", Password));

        var refreshed = await service.RefreshAsync(login.RefreshToken);

        Assert.False(string.IsNullOrWhiteSpace(refreshed.Token));
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

        var old = await db.RefreshTokens.SingleAsync(t => t.TokenHash == JwtService.HashRefreshToken(login.RefreshToken));
        Assert.NotNull(old.RevokedAt);
        Assert.Equal(JwtService.HashRefreshToken(refreshed.RefreshToken), old.ReplacedByTokenHash);

        var fresh = await db.RefreshTokens.SingleAsync(t => t.TokenHash == JwtService.HashRefreshToken(refreshed.RefreshToken));
        Assert.Null(fresh.RevokedAt);
    }

    [Fact]
    public async Task Refresh_ReusedToken_IsRejected_AndRevokesAllUserSessions()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var clock = new MutableClock();
        var service = CreateAuthService(db, clock);

        // İki ayrı cihaz/oturum.
        var laptop = await service.LoginAsync(new LoginRequest("sefa", Password));
        var phone  = await service.LoginAsync(new LoginRequest("sefa", Password));

        var rotated = await service.RefreshAsync(laptop.RefreshToken);

        // Eski (zaten kullanılmış) laptop token'ı tolerans penceresi geçtikten sonra tekrar geliyor: çalınma belirtisi.
        clock.Advance(TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(laptop.RefreshToken));

        Assert.All(await db.RefreshTokens.ToListAsync(), t => Assert.NotNull(t.RevokedAt));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(rotated.RefreshToken));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(phone.RefreshToken));
    }

    [Fact]
    public async Task Refresh_ReusedToken_DoesNotAffectOtherUsers()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        await SeedUserAsync(db, "baska", "baska@test.com");
        var clock = new MutableClock();
        var service = CreateAuthService(db, clock);
        var sefa  = await service.LoginAsync(new LoginRequest("sefa", Password));
        var baska = await service.LoginAsync(new LoginRequest("baska", Password));

        await service.RefreshAsync(sefa.RefreshToken);
        clock.Advance(TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(sefa.RefreshToken));

        var other = await service.RefreshAsync(baska.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(other.Token));
    }

    [Fact]
    public async Task Refresh_ExpiredToken_IsRejected()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateAuthService(db);
        var login = await service.LoginAsync(new LoginRequest("sefa", Password));

        var stored = await db.RefreshTokens.SingleAsync();
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(login.RefreshToken));
    }

    [Theory]
    [InlineData("olmayan-token")]
    [InlineData("")]
    public async Task Refresh_UnknownOrEmptyToken_IsRejected(string token)
    {
        await using var db = CreateDb();
        var service = CreateAuthService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(token));
    }

    // ---------------- Tolerans penceresi (#100) ----------------

    [Fact]
    public async Task Refresh_ReusedInsideGraceWindow_IsRejected_ButOtherSessionsStayValid()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var clock = new MutableClock();
        var service = CreateAuthService(db, clock);
        var laptop = await service.LoginAsync(new LoginRequest("sefa", Password));
        var phone  = await service.LoginAsync(new LoginRequest("sefa", Password));

        // Sekme A yeniledi; sekme B aynı eski token'la 5 sn sonra geliyor.
        var rotated = await service.RefreshAsync(laptop.RefreshToken);
        clock.Advance(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<RefreshTokenAlreadyRotatedException>(() => service.RefreshAsync(laptop.RefreshToken));

        // Kimse atılmadı: sekme A'nın yeni token'ı ve telefon hâlâ çalışıyor.
        Assert.False(string.IsNullOrWhiteSpace((await service.RefreshAsync(rotated.RefreshToken)).Token));
        Assert.False(string.IsNullOrWhiteSpace((await service.RefreshAsync(phone.RefreshToken)).Token));
    }

    [Fact]
    public async Task Refresh_ReusedJustAfterGraceWindow_RevokesAllSessions()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var clock = new MutableClock();
        var service = CreateAuthService(db, clock);
        var laptop = await service.LoginAsync(new LoginRequest("sefa", Password));
        var rotated = await service.RefreshAsync(laptop.RefreshToken);

        clock.Advance(TimeSpan.FromSeconds(11));

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(laptop.RefreshToken));
        Assert.IsNotType<RefreshTokenAlreadyRotatedException>(ex);
        Assert.All(await db.RefreshTokens.ToListAsync(), t => Assert.NotNull(t.RevokedAt));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(rotated.RefreshToken));
    }

    [Fact]
    public async Task Refresh_LoggedOutTokenInsideGraceWindow_StillRevokesAllSessions()
    {
        // Pencere yalnızca rotasyon için: çıkışla iptal edilen token'ın tekrar gelmesi hep şüpheli.
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var clock = new MutableClock();
        var service = CreateAuthService(db, clock);
        var laptop = await service.LoginAsync(new LoginRequest("sefa", Password));
        var phone  = await service.LoginAsync(new LoginRequest("sefa", Password));

        await service.LogoutAsync(laptop.RefreshToken);
        clock.Advance(TimeSpan.FromSeconds(2));

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(laptop.RefreshToken));
        Assert.IsNotType<RefreshTokenAlreadyRotatedException>(ex);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(phone.RefreshToken));
    }

    // ---------------- Logout ----------------

    [Fact]
    public async Task Logout_RevokesToken_SoItCanNoLongerRefresh()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateAuthService(db);
        var login = await service.LoginAsync(new LoginRequest("sefa", Password));

        await service.LogoutAsync(login.RefreshToken);

        Assert.NotNull((await db.RefreshTokens.SingleAsync()).RevokedAt);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(login.RefreshToken));
    }

    [Fact]
    public async Task Logout_OnlyRevokesGivenSession()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var service = CreateAuthService(db);
        var laptop = await service.LoginAsync(new LoginRequest("sefa", Password));
        var phone  = await service.LoginAsync(new LoginRequest("sefa", Password));

        await service.LogoutAsync(laptop.RefreshToken);

        var stillWorks = await service.RefreshAsync(phone.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(stillWorks.Token));
    }

    [Fact]
    public async Task Logout_UnknownToken_DoesNotThrow()
    {
        await using var db = CreateDb();
        var service = CreateAuthService(db);

        await service.LogoutAsync("olmayan-token");
    }

    // ---------------- Şifre değişimi / sıfırlama ----------------

    [Fact]
    public async Task ChangePassword_RevokesAllRefreshTokens()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db);
        var service = CreateAuthService(db);
        var laptop = await service.LoginAsync(new LoginRequest("sefa", Password));
        var phone  = await service.LoginAsync(new LoginRequest("sefa", Password));

        await new UserService(db).ChangePasswordAsync(user.Id, new ChangePasswordRequest(Password, "YeniSifre456!"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(laptop.RefreshToken));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(phone.RefreshToken));
    }

    [Fact]
    public async Task ResetPassword_RevokesAllRefreshTokens()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db);
        var service = CreateAuthService(db);
        var login = await service.LoginAsync(new LoginRequest("sefa", Password));

        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            Token = "sifirla",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        });
        await db.SaveChangesAsync();

        await service.ResetPasswordAsync("sifirla", "YeniSifre456!");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RefreshAsync(login.RefreshToken));
    }

    // ---------------- HTTP uçları ----------------

    [Fact]
    public async Task RefreshEndpoint_ReturnsNewTokens_AndReuseReturns401()
    {
        // Tolerans penceresi kapalı: tekrar kullanım hemen saldırı sayılır.
        using var factory = new RefreshTokenApiFactory(reuseGraceSeconds: 0);
        await factory.SeedUserAsync();
        var client = factory.CreateClient();

        var login = await LoginAsync(client);

        var refresh = await client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = (await refresh.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotEqual(login.RefreshToken, rotated.RefreshToken);

        // Yeni access token korumalı uçta çalışıyor.
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/Auth/me");
        meRequest.Headers.Authorization = new("Bearer", rotated.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(meRequest)).StatusCode);

        var reuse = await client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // Yeniden kullanım tespit edildiği için rotation'la gelen token da artık geçersiz.
        var afterReuse = await client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task RefreshEndpoint_SecondTabInsideGraceWindow_Gets401WithCode_AndStaysLoggedIn()
    {
        using var factory = new RefreshTokenApiFactory();
        await factory.SeedUserAsync();
        var client = factory.CreateClient();
        var login = await LoginAsync(client);

        var first = await client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken = login.RefreshToken });
        var rotated = (await first.Content.ReadFromJsonAsync<AuthResponse>())!;

        // İkinci sekme aynı anda eski token'la geliyor.
        var second = await client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal(RefreshTokenAlreadyRotatedException.ErrorCode, body!["code"]);

        // İlk sekmenin aldığı token hâlâ geçerli.
        var again = await client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task LogoutEndpoint_WorksWithoutAccessToken_AndTokenStopsWorking()
    {
        using var factory = new RefreshTokenApiFactory();
        await factory.SeedUserAsync();
        var client = factory.CreateClient();
        var login = await LoginAsync(client);

        var logout = await client.PostAsJsonAsync("/api/Auth/logout", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task RefreshEndpoint_IsRateLimited()
    {
        // Varsayılan auth politikası: IP başına dakikada 5 istek.
        using var factory = new RateLimitApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken = "yok" });

        var blocked = await client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken = "yok" });

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    // ---------------- Yardımcılar ----------------

    private static async Task<AuthResponse> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/Auth/login", new { usernameOrEmail = "sefa", password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IConfiguration CreateConfig(int accessTokenMinutes = 15) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "TEST_KEY_THAT_IS_AT_LEAST_32_CHARACTERS_LONG",
                ["Jwt:Issuer"] = "test",
                ["Jwt:Audience"] = "test",
                ["Jwt:AccessTokenMinutes"] = accessTokenMinutes.ToString(),
                ["Jwt:RefreshTokenDays"] = "7"
            })
            .Build();

    private static AuthService CreateAuthService(AppDbContext db, IClock? clock = null)
    {
        var config = CreateConfig();
        return new AuthService(db, new JwtService(config), NullLogger<AuthService>.Instance, configuration: config, clock: clock);
    }

    /// <summary>Elle ilerletilebilen saat; tolerans penceresinin içini ve dışını denemek için.</summary>
    internal sealed class MutableClock : IClock
    {
        public DateTime UtcNow { get; private set; } = DateTime.UtcNow;
        public void Advance(TimeSpan by) => UtcNow += by;
    }

    internal static async Task<User> SeedUserAsync(AppDbContext db, string username = "sefa", string email = "sefa@test.com")
    {
        var user = new User
        {
            Username = username,
            Email = email,
            // workFactor 4: test hızlı çalışsın diye düşük maliyet (üretimde kullanılmaz).
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4),
            EmailConfirmed = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}

public class RefreshTokenApiFactory(int? reuseGraceSeconds = null) : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (reuseGraceSeconds is not null)
            builder.UseSetting("Jwt:RefreshReuseGraceSeconds", reuseGraceSeconds.Value.ToString());
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

    public async Task SeedUserAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await RefreshTokenTests.SeedUserAsync(db);
    }
}
