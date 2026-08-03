using CryptoTracker.API.DTOs;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CryptoTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BotController(IBotService botService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMyBots(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var bots = await botService.GetBotsByUserAsync(userId, cancellationToken);
        return Ok(bots);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBotRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var bot = await botService.CreateBotAsync(userId, request, cancellationToken);
        return Ok(bot);
    }

    [HttpPatch("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var bot = await botService.ToggleBotAsync(userId, id, cancellationToken);
        return Ok(bot);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        await botService.DeleteBotAsync(userId, id, cancellationToken);
        return NoContent(); // Veya duruma göre Ok(new { message = "Bot başarıyla silindi." }) dönebilirsiniz.
    }

    [HttpGet("{id:int}/signals")]
    public async Task<IActionResult> GetSignals(int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var signals = await botService.GetSignalsAsync(userId, id, cancellationToken);
        return Ok(signals);
    }

    [HttpPost("signals/{signalId:int}/approve")]
    public async Task<IActionResult> Approve(int signalId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await botService.ApproveSignalAsync(userId, signalId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("signals/{signalId:int}/reject")]
    public async Task<IActionResult> Reject(int signalId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await botService.RejectSignalAsync(userId, signalId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("performance")]
    public async Task<IActionResult> GetPerformance(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var performance = await botService.GetBotPerformanceAsync(userId, cancellationToken);
        return Ok(performance);
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}