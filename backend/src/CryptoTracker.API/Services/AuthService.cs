using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace CryptoTracker.API.Services;

public class AuthService(AppDbContext db, IJwtService jwtService, ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
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

        var roles = await db.Set<UserRole>()
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        string token = jwtService.GenerateToken(user, roles);

        return new AuthResponse(token, user.Username, roles);
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

        logger.LogInformation("Password reset token for {Email}: {Token}", user.Email, token);

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
