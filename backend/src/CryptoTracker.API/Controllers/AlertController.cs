using CryptoTracker.API.DTOs;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CryptoTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlertController(IAlertService alertService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAlertRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await alertService.CreateAsync(userId, request);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetMyAlerts()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await alertService.GetByUserAsync(userId);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        await alertService.DeleteAsync(userId, id);
        return NoContent();
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}
