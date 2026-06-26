using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class RoleService(AppDbContext db) : IRoleService
{
    public async Task<IEnumerable<RoleDto>> GetAllAsync()
    {
        return await db.Roles.Select(r => new RoleDto(r.Id, r.Name)).ToListAsync();
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Rol adı boş olamaz.");
        }

        var exists = await db.Roles.AnyAsync(r => r.Name == request.Name);
        if (exists)
        {
            throw new InvalidOperationException("Bu isimde bir rol zaten mevcut.");
        }

        var role = new Role { Name = request.Name };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        return new RoleDto(role.Id, role.Name);
    }

    public async Task AssignRoleAsync(int userId, int roleId)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        }

        var roleExists = await db.Roles.AnyAsync(r => r.Id == roleId);
        if (!roleExists)
        {
            throw new KeyNotFoundException("Rol bulunamadı.");
        }

        var alreadyAssigned = await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (alreadyAssigned)
        {
            throw new InvalidOperationException("Kullanıcı zaten bu role sahip.");
        }

        var userRole = new UserRole { UserId = userId, RoleId = roleId };
        db.UserRoles.Add(userRole);
        await db.SaveChangesAsync();
    }

    public async Task RemoveRoleAsync(int userId, int roleId)
    {
        var userRole = await db.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (userRole == null)
        {
            throw new KeyNotFoundException("Kullanıcı belirtilen role sahip değil.");
        }

        db.UserRoles.Remove(userRole);
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<string>> GetUserRolesAsync(int userId)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        }

        return await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync();
    }
}
