using CryptoTracker.API.DTOs;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class RoleController(IRoleService roleService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await roleService.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
        => Ok(await roleService.CreateAsync(request));

    [HttpPost("{roleId}/assign/{userId}")]
    public async Task<IActionResult> Assign(int roleId, int userId)
    {
        await roleService.AssignRoleAsync(userId, roleId);
        return NoContent();
    }

    [HttpDelete("{roleId}/remove/{userId}")]
    public async Task<IActionResult> Remove(int roleId, int userId)
    {
        await roleService.RemoveRoleAsync(userId, roleId);
        return NoContent();
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserRoles(int userId)
        => Ok(await roleService.GetUserRolesAsync(userId));
}
