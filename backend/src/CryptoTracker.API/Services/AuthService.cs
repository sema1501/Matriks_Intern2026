using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class AuthService(AppDbContext db, IJwtService jwtService) : IAuthService
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
}
