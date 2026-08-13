using CryptoTracker.API.DTOs;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CryptoTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BotController(
    IBotService botService,
    IBacktestService backtestService,
    IBotDebugExecuteService debugExecuteService,
    IDebugEndpointAccess debugEndpointAccess) : ControllerBase
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

    /// <summary>
    /// Legacy manual approve for historical Pending signals only.
    /// New bot signals are auto-executed by BotMonitorService and never require this.
    /// </summary>
    [HttpPost("signals/{signalId:int}/approve")]
    public async Task<IActionResult> Approve(int signalId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await botService.ApproveSignalAsync(userId, signalId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Legacy manual reject for historical Pending signals only.
    /// </summary>
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

    /// <summary>
    /// Pure historical RSI backtest. Never places orders or mutates portfolio/bot state.
    /// </summary>
    [HttpPost("{id:int}/backtest")]
    public async Task<IActionResult> Backtest(
        int id,
        [FromBody] BacktestRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await backtestService.RunAsync(userId, id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// DEVELOPMENT / DEBUG ONLY.
    /// Forces a virtual portfolio BUY/SELL for smoke-testing automatic execution.
    /// Unavailable outside Development (returns 404). Does not place real/Testnet orders.
    /// </summary>
    [HttpPost("{id:int}/debug/execute")]
    public async Task<IActionResult> DebugExecute(
        int id,
        [FromBody] DebugBotExecuteRequest request,
        CancellationToken cancellationToken)
    {
        if (!debugEndpointAccess.AllowDebugExecute)
            return NotFound();

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await debugExecuteService.ExecuteAsync(
            userId, id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// DEVELOPMENT / DEBUG ONLY.
    /// Proves RsiSignalEvaluator.DetermineZoneEntrySignal + BotAutoTradeExecutor together.
    /// Unavailable outside Development (returns 404). Virtual portfolio only.
    /// </summary>
    [HttpPost("{id:int}/debug/zone-entry")]
    public async Task<IActionResult> DebugZoneEntry(
        int id,
        [FromBody] DebugZoneEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (!debugEndpointAccess.AllowDebugExecute)
            return NotFound();

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await debugExecuteService.EvaluateZoneEntryAsync(
            userId, id, request, cancellationToken);
        return Ok(result);
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}