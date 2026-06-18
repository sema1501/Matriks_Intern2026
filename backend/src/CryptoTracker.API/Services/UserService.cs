using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class UserService(AppDbContext db) : IUserService
{
    public async Task<UserDto> GetByIdAsync(int id)
    {
        // TODO — Gorev 2
        // db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
        //         .FirstOrDefaultAsync(u => u.Id == id) -> UserDto'ya map et
        throw new NotImplementedException("Gorev 2: GetByIdAsync implement edilmedi.");
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        // TODO — Gorev 2
        throw new NotImplementedException("Gorev 2: GetAllAsync implement edilmedi.");
    }

    public async Task<UserDto> UpdateProfileAsync(int id, UpdateProfileRequest request)
    {
        // TODO — Gorev 2
        throw new NotImplementedException("Gorev 2: UpdateProfileAsync implement edilmedi.");
    }

    public async Task ChangePasswordAsync(int id, ChangePasswordRequest request)
    {
        // TODO — Gorev 2
        // BCrypt.Verify ile mevcut sifreyi dogrula, yeni sifreyi hashle ve kaydet
        throw new NotImplementedException("Gorev 2: ChangePasswordAsync implement edilmedi.");
    }
}
