using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class RoleService(AppDbContext db) : IRoleService
{
    public async Task<IEnumerable<RoleDto>> GetAllAsync()
    {
        // TODO — Gorev 3
        throw new NotImplementedException("Gorev 3: GetAllAsync implement edilmedi.");
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request)
    {
        // TODO — Gorev 3
        throw new NotImplementedException("Gorev 3: CreateAsync implement edilmedi.");
    }

    public async Task AssignRoleAsync(int userId, int roleId)
    {
        // TODO — Gorev 3
        throw new NotImplementedException("Gorev 3: AssignRoleAsync implement edilmedi.");
    }

    public async Task RemoveRoleAsync(int userId, int roleId)
    {
        // TODO — Gorev 3
        throw new NotImplementedException("Gorev 3: RemoveRoleAsync implement edilmedi.");
    }

    public async Task<IEnumerable<string>> GetUserRolesAsync(int userId)
    {
        // TODO — Gorev 3
        throw new NotImplementedException("Gorev 3: GetUserRolesAsync implement edilmedi.");
    }
}
