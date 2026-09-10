using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace CryptoTracker.API.Services;

public class AuthService(
    AppDbContext db,
    IJwtService jwtService,
    ILogger<AuthService> logger,
    IEmailSender? emailSender = null,
    IConfiguration? configuration = null) : IAuthService
{
    public async Task<string> RegisterAsync(RegisterRequest request)
    {
        bool emailExists    = await db.Users.AnyAsync(u => u.Email    == request.Email);
        bool usernameExists = await db.Users.AnyAsync(u => u.Username == request.Username);

        if (emailExists)    throw new Exception("Bu email zaten kullanımda.");
        if (usernameExists) throw new Exception("Bu kullanıcı adı zaten kullanımda.");

        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            Username     = request.Username,
            Email        = request.Email,
            PasswordHash = passwordHash,
            VirtualBalance = 10_000m,
            EmailConfirmed = false, // Doğrulanana kadar giriş yapılamaz (Görev 48)
            CreatedAt    = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var userRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "User");
        if (userRole != null)
        {
            db.Set<UserRole>().Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
            await db.SaveChangesAsync();
        }

        // Doğrulama token'ı üret ve doğrulama e-postası gönder (Görev 48; Görev 40 altyapısı).
        string verificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            UserId = user.Id,
            Token = verificationToken,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            IsUsed = false
        });
        await db.SaveChangesAsync();

        await SendVerificationEmailAsync(user, verificationToken);

        return "Kaydınız alındı. Hesabınızı etkinleştirmek için e-postanıza gönderilen doğrulama bağlantısına tıklayın.";
    }

    private async Task SendVerificationEmailAsync(User user, string token)
    {
        var baseUrl = configuration?["App:FrontendBaseUrl"] ?? "http://localhost:3000";
        var link = $"{baseUrl}/confirm-email/{token}";
        var subject = "CryptoTracker - E-posta Doğrulama";
        var body =
            $"Merhaba {user.Username},\n\n" +
            "CryptoTracker hesabını etkinleştirmek için aşağıdaki bağlantıya tıkla:\n" +
            $"{link}\n\n" +
            "Bu bağlantı 24 saat geçerlidir. Bu kaydı sen yapmadıysan bu e-postayı yok sayabilirsin.\n";

        if (emailSender is not null)
            await emailSender.SendAsync(user.Email, subject, body);
        else
            logger.LogInformation("E-posta doğrulama bağlantısı ({Email}): {Link}", user.Email, link);
    }

    public async Task ConfirmEmailAsync(string token)
    {
        var verification = await db.EmailVerificationTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token);

        if (verification == null) throw new ArgumentException("Doğrulama bağlantısı geçersiz.");
        if (verification.IsUsed) throw new InvalidOperationException("Bu doğrulama bağlantısı zaten kullanılmış.");
        if (verification.ExpiresAt <= DateTime.UtcNow) throw new InvalidOperationException("Doğrulama bağlantısının süresi dolmuş.");

        verification.User.EmailConfirmed = true;
        verification.IsUsed = true;
        await db.SaveChangesAsync();
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u =>
                u.Email    == request.UsernameOrEmail ||
                u.Username == request.UsernameOrEmail);

        if (user == null) throw new Exception("Kullanıcı bulunamadı.");

        bool valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!valid) throw new Exception("Şifre hatalı.");

        // Doğrulanmamış hesaplar giriş yapamaz (Görev 48).
        if (!user.EmailConfirmed)
            throw new InvalidOperationException("Lütfen önce e-posta adresinizi doğrulayın. Doğrulama bağlantısı kayıt sırasında e-postanıza gönderildi.");

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        string token = jwtService.GenerateToken(user, roles);

        return new AuthResponse(token, user.Username, roles);
    }

    public async Task<string> ForgotPasswordAsync(string email)
    {
        const string message = "Eğer bu e-posta kayıtlıysa şifre sıfırlama bağlantısı oluşturuldu.";

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email.Trim());
        if (user == null) return message;

        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            IsUsed = false
        };

        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        // Sıfırlama bağlantısını gerçek e-postayla gönder (Görev 50; Görev 40 altyapısını kullanır).
        // SMTP yapılandırılmamışsa geliştirme için bağlantı loglanır.
        var baseUrl = configuration?["App:FrontendBaseUrl"] ?? "http://localhost:3000";
        var link = $"{baseUrl}/reset-password/{token}";
        var subject = "CryptoTracker - Şifre Sıfırlama";
        var body =
            $"Merhaba {user.Username},\n\n" +
            "Şifreni sıfırlamak için aşağıdaki bağlantıya tıkla:\n" +
            $"{link}\n\n" +
            "Bu bağlantı 30 dakika geçerlidir. Bu talebi sen yapmadıysan bu e-postayı yok sayabilirsin.\n";

        if (emailSender is not null)
            await emailSender.SendAsync(user.Email, subject, body);
        else
            logger.LogInformation("Şifre sıfırlama bağlantısı ({Email}): {Link}", user.Email, link);

        return message;
    }

    public async Task ResetPasswordAsync(string token, string newPassword)
    {
        var resetToken = await db.PasswordResetTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token);

        if (resetToken == null) throw new ArgumentException("Şifre sıfırlama tokenı geçersiz.");
        if (resetToken.IsUsed) throw new InvalidOperationException("Bu token daha önce kullanılmış.");
        if (resetToken.ExpiresAt <= DateTime.UtcNow) throw new InvalidOperationException("Tokenın süresi dolmuş.");
        if (BCrypt.Net.BCrypt.Verify(newPassword, resetToken.User.PasswordHash))throw new InvalidOperationException("Yeni şifre eski şifre ile aynı olamaz.");

        resetToken.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        resetToken.IsUsed = true;

        await db.SaveChangesAsync();
    }
}
