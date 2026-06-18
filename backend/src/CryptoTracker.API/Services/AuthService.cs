using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class AuthService(AppDbContext db, IJwtService jwtService) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // TODO — Görev 1
        // 1. Email/username benzersizligini kontrol et (db.Users.AnyAsync)
        // 2. BCrypt.Net.BCrypt.HashPassword(request.Password) ile hashle
        // 3. User nesnesi olustur, db'ye kaydet
        // 4. "User" rolunu bul ve UserRole olarak ata
        // 5. jwtService.GenerateToken() ile token uret
        // 6. AuthResponse dondur
        throw new NotImplementedException("Gorev 1: RegisterAsync implement edilmedi.");
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // TODO — Görev 1
        // 1. db.Users'da UsernameOrEmail ile kullanici bul (hem username hem email kontrol et)
        // 2. BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash) ile dogrula
        // 3. Kullanicinin rollerini cek (UserRoles -> Role)
        // 4. jwtService.GenerateToken() ile token uret
        // 5. AuthResponse dondur
        throw new NotImplementedException("Gorev 1: LoginAsync implement edilmedi.");
    }
}
