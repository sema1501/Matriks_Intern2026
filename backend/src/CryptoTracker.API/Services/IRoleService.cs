using CryptoTracker.API.DTOs;
namespace CryptoTracker.API.Services;
public interface IRoleService
{
    Task<IEnumerable<RoleDto>> GetAllAsync();
    Task<RoleDto> CreateAsync(CreateRoleRequest request);
    Task AssignRoleAsync(int userId, int roleId);
    Task RemoveRoleAsync(int userId, int roleId);
    Task<IEnumerable<string>> GetUserRolesAsync(int userId);
}
