using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CryptoTracker.API.Tests;

/// <summary>
/// #82 — Görev 48 e-posta doğrulama akışı (AuthService.ConfirmEmailAsync + LoginAsync)
/// ve Görev 43/47 profil fotoğrafı boyut sınırı (UserService.SetAvatarAsync).
/// </summary>
public class EmailVerificationTests
{
    private const string Password = "Sifre123!";

    // ---------------- ConfirmEmailAsync ----------------

    [Fact]
    public async Task ConfirmEmail_UnknownToken_ThrowsArgumentException()
    {
        await using var db = CreateDb();
        var service = CreateAuthService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConfirmEmailAsync("olmayan-token"));

        Assert.Contains("geçersiz", ex.Message);
    }

    [Fact]
    public async Task ConfirmEmail_AlreadyUsedToken_ThrowsInvalidOperation()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, emailConfirmed: false);
        await SeedTokenAsync(db, user, "kullanilmis", DateTime.UtcNow.AddHours(1), isUsed: true);
        var service = CreateAuthService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmEmailAsync("kullanilmis"));

        Assert.Contains("zaten kullanılmış", ex.Message);
        Assert.False((await db.Users.AsNoTracking().SingleAsync()).EmailConfirmed);
    }

    [Fact]
    public async Task ConfirmEmail_ExpiredToken_ThrowsInvalidOperation()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, emailConfirmed: false);
        await SeedTokenAsync(db, user, "suresi-dolmus", DateTime.UtcNow.AddMinutes(-1), isUsed: false);
        var service = CreateAuthService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmEmailAsync("suresi-dolmus"));

        Assert.Contains("süresi dolmuş", ex.Message);
        Assert.False((await db.Users.AsNoTracking().SingleAsync()).EmailConfirmed);
    }

    [Fact]
    public async Task ConfirmEmail_ValidToken_ConfirmsUser_AndConsumesToken()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, emailConfirmed: false);
        await SeedTokenAsync(db, user, "gecerli", DateTime.UtcNow.AddHours(1), isUsed: false);
        var service = CreateAuthService(db);

        await service.ConfirmEmailAsync("gecerli");

        Assert.True((await db.Users.AsNoTracking().SingleAsync()).EmailConfirmed);
        Assert.True((await db.EmailVerificationTokens.AsNoTracking().SingleAsync()).IsUsed);

        // Aynı bağlantı ikinci kez kullanılamaz.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmEmailAsync("gecerli"));
    }

    // ---------------- LoginAsync ----------------

    [Fact]
    public async Task Login_UnconfirmedEmail_IsRejected()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db, emailConfirmed: false);
        var service = CreateAuthService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.LoginAsync(new LoginRequest("sefa", Password)));

        Assert.Contains("doğrulayın", ex.Message);
    }

    [Fact]
    public async Task Login_ConfirmedEmail_ReturnsToken()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db, emailConfirmed: true);
        var service = CreateAuthService(db);

        var response = await service.LoginAsync(new LoginRequest("sefa@test.com", Password));

        Assert.Equal("fake-jwt", response.Token);
    }

    // ---------------- SetAvatarAsync ----------------

    [Fact]
    public async Task SetAvatar_Over4MB_IsRejected_AndNotSaved()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, emailConfirmed: true);
        var service = new UserService(db);

        var tooBig = "data:image/png;base64," + new string('A', 4_000_000);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetAvatarAsync(user.Id, tooBig));
        Assert.Null((await db.Users.AsNoTracking().SingleAsync()).AvatarUrl);
    }

    [Fact]
    public async Task SetAvatar_AtLimit_IsAccepted()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, emailConfirmed: true);
        var service = new UserService(db);

        var atLimit = new string('A', 4_000_000); // sınır dahil: > 4_000_000 reddedilir

        var dto = await service.SetAvatarAsync(user.Id, atLimit);

        Assert.Equal(atLimit, dto.AvatarUrl);
    }

    [Fact]
    public async Task SetAvatar_Null_RemovesPhoto()
    {
        await using var db = CreateDb();
        var user = await SeedUserAsync(db, emailConfirmed: true);
        var service = new UserService(db);
        await service.SetAvatarAsync(user.Id, "data:image/png;base64,AAAA");

        var dto = await service.SetAvatarAsync(user.Id, null);

        Assert.Null(dto.AvatarUrl);
    }

    // ---------------- Yardımcılar ----------------

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AuthService CreateAuthService(AppDbContext db)
    {
        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()))
            .Returns("fake-jwt");

        return new AuthService(db, jwt.Object, NullLogger<AuthService>.Instance);
    }

    private static async Task<User> SeedUserAsync(AppDbContext db, bool emailConfirmed)
    {
        var user = new User
        {
            Username = "sefa",
            Email = "sefa@test.com",
            // workFactor 4: test hızlı çalışsın diye düşük maliyet (üretimde kullanılmaz).
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4),
            EmailConfirmed = emailConfirmed
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task SeedTokenAsync(
        AppDbContext db, User user, string token, DateTime expiresAt, bool isUsed)
    {
        db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            UserId = user.Id,
            Token = token,
            ExpiresAt = expiresAt,
            IsUsed = isUsed
        });
        await db.SaveChangesAsync();
    }
}
