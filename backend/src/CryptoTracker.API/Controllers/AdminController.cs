using System.Security.Claims;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.API.Controllers;

[ApiController]
[Route("api/Admin")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminController(
    IAdminBotService adminBotService,
    IAuditLogService auditLogService) : ControllerBase
{
    [HttpPatch("Bot/{id:int}/force-stop")]
    public async Task<IActionResult> ForceStop(
        int id,
        [FromBody] AdminBotActionRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actorUserId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        await adminBotService.ForceStopAsync(actorUserId, id, request?.AdminNote, cancellationToken);
        return Ok(new { message = "Bot zorla durduruldu." });
    }

    [HttpPatch("Bot/{id:int}/flag")]
    public async Task<IActionResult> Flag(
        int id,
        [FromBody] AdminBotActionRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actorUserId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        await adminBotService.FlagAsync(actorUserId, id, request?.AdminNote, cancellationToken);
        return Ok(new { message = "Bot şüpheli olarak işaretlendi." });
    }

    [HttpGet("audit-log")]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool ascending = false,
        CancellationToken cancellationToken = default)
    {
        var logs = await auditLogService.GetAsync(from, to, ascending, cancellationToken);
        return Ok(logs);
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}
