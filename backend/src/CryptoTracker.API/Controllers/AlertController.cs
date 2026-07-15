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
    public async Task<IActionResult> Create([FromBody] CreateAlertRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await alertService.CreateAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetMyAlerts(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await alertService.GetByUserAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        await alertService.DeleteAsync(userId, id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/signals")]
    public async Task<IActionResult> GetSignals(int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await alertService.GetSignalsAsync(userId, id, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id, [FromBody] ToggleAlertRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await alertService.ToggleAsync(userId, id, request, cancellationToken);
        return Ok(result);
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}
