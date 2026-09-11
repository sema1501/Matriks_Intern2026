using CryptoTracker.API.DTOs;
namespace CryptoTracker.API.Services;
public interface IUserService
{
    Task<UserDto> GetByIdAsync(int id);
    Task<IEnumerable<UserDto>> GetAllAsync();
    Task<UserDto> UpdateProfileAsync(int id, UpdateProfileRequest request);
    Task ChangePasswordAsync(int id, ChangePasswordRequest request);
    Task<UserDto> SetEmailNotificationsAsync(int id, bool enabled);
    Task<UserDto> SetAvatarAsync(int id, string? avatarUrl);
}
