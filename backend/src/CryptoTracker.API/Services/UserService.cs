using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class UserService(AppDbContext db) : IUserService
{
    public async Task<UserDto> GetByIdAsync(int id)
    {
        var user = await db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) throw new Exception("Kullanıcı bulunamadı.");

        return MapToDto(user);
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        var users = await db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .ToListAsync();

        return users.Select(MapToDto);
    }

    public async Task<UserDto> UpdateProfileAsync(int id, UpdateProfileRequest request)
    {
        var user = await db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) throw new Exception("Kullanıcı bulunamadı.");

        if (!string.IsNullOrWhiteSpace(request.Username) && request.Username != user.Username)
        {
            if (await db.Users.AnyAsync(u => u.Username == request.Username && u.Id != id))
                throw new Exception("Bu kullanıcı adı zaten kullanımda.");
            user.Username = request.Username;
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            if (await db.Users.AnyAsync(u => u.Email == request.Email && u.Id != id))
                throw new Exception("Bu email zaten kullanımda.");
            user.Email = request.Email;
        }

        await db.SaveChangesAsync();
        return MapToDto(user);
    }

    public async Task ChangePasswordAsync(int id, ChangePasswordRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) throw new Exception("Kullanıcı bulunamadı.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new Exception("Mevcut şifre hatalı.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await db.SaveChangesAsync();
    }

    private static UserDto MapToDto(User user) =>
        new(user.Id, user.Username, user.Email,
            user.UserRoles.Select(ur => ur.Role.Name),
            user.CreatedAt);
}
