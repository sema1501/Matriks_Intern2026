using System.Security.Claims;
using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditLogService _auditLogService;
    private const int OvertradeWindowMinutes = 15;
    private const int OvertradeSignalThreshold = 5;
    private const int MaxNoteLength = 500;
    private const int MaxDetailsLength = 2000;

    public AdminController(AppDbContext context, IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    [HttpGet("bots")]
    public async Task<ActionResult<List<AdminBotDto>>> GetBots()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-OvertradeWindowMinutes);

        var bots = await _context.TradingBots
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new AdminBotDto(
                b.Id,
                b.UserId,
                b.User.Username,
                b.Symbol,
                "RSI",
                b.IsActive,
                b.BuyRsiThreshold,
                b.SellRsiThreshold,
                b.TradeQuantity,
                b.CreatedAt,
                b.Signals.Count(s => s.Status == BotSignalStatus.Approved && s.CreatedAt >= cutoff),
                b.Signals.Count(s => s.Status == BotSignalStatus.Approved && s.CreatedAt >= cutoff) >= OvertradeSignalThreshold
            ))
            .ToListAsync();

        return Ok(bots);
    }

    [HttpPost("bots/{botId:int}/kill")]
    public async Task<IActionResult> KillBot(int botId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actorUserId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var bot = await _context.TradingBots.FirstOrDefaultAsync(b => b.Id == botId, cancellationToken);
        if (bot is null)
            return NotFound(new { message = "Bot bulunamadı." });

        if (!bot.IsActive)
            return Ok(new { botId = bot.Id, isActive = false, message = "Bot zaten durdurulmuş." });

        bot.IsActive = false;
        _context.AuditLogs.Add(CreateLog(
            actorUserId,
            AuditLogActions.BotForceStopped,
            bot,
            adminNote: null,
            "Bot zorla durduruldu."));

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            botId = bot.Id,
            isActive = bot.IsActive,
            message = $"{bot.Symbol} botu admin tarafından zorla durduruldu."
        });
    }

    [HttpGet("overtrading")]
    public async Task<ActionResult<List<OvertradingBotDto>>> GetOvertradingBots()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-OvertradeWindowMinutes);

        var riskyBots = await _context.TradingBots
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new
            {
                Bot = b,
                RecentTradeCount = b.Signals.Count(s =>
                    s.Status == BotSignalStatus.Approved && s.CreatedAt >= cutoff)
            })
            .Where(x => x.RecentTradeCount >= OvertradeSignalThreshold)
            .OrderByDescending(x => x.RecentTradeCount)
            .Select(x => new OvertradingBotDto(
                x.Bot.Id,
                x.Bot.UserId,
                x.Bot.User.Username,
                x.Bot.Symbol,
                x.RecentTradeCount,
                OvertradeWindowMinutes,
                OvertradeSignalThreshold
            ))
            .ToListAsync();

        return Ok(riskyBots);
    }

    [HttpGet("portfolios")]
    public async Task<ActionResult<List<AdminPortfolioDto>>> GetPortfolios()
    {
        var portfolios = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .Select(u => new AdminPortfolioDto(
                u.Id,
                u.Username,
                u.VirtualBalance,
                u.Holdings
                    .OrderBy(h => h.Symbol)
                    .Select(h => new AdminHoldingDto(
                        h.Id,
                        h.Symbol,
                        h.Quantity,
                        h.AvgBuyPrice
                    ))
                    .ToList()
            ))
            .ToListAsync();

        return Ok(portfolios);
    }

    [HttpPatch("Bot/{id:int}/flag")]
    public async Task<IActionResult> Flag(
        int id,
        [FromBody] AdminBotActionRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var actorUserId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var bot = await _context.TradingBots.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bot is null)
            return NotFound(new { message = "Bot bulunamadı." });

        bot.IsFlagged = true;
        _context.AuditLogs.Add(CreateLog(
            actorUserId,
            AuditLogActions.BotFlagged,
            bot,
            request?.AdminNote,
            "Bot şüpheli olarak işaretlendi."));

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Bot şüpheli olarak işaretlendi." });
    }

    [HttpGet("audit-log")]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool ascending = false,
        CancellationToken cancellationToken = default)
    {
        var logs = await _auditLogService.GetAsync(from, to, ascending, cancellationToken);
        return Ok(logs);
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }

    private static AuditLog CreateLog(
        int actorUserId,
        string action,
        TradingBot bot,
        string? adminNote,
        string summary)
    {
        var note = SanitizeNote(adminNote);
        var details = $"{summary} BotId={bot.Id}; Symbol={bot.Symbol}; OwnerUserId={bot.UserId}.";
        if (note is not null)
            details += $" Note={note}";

        if (details.Length > MaxDetailsLength)
            details = details[..MaxDetailsLength];

        return new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            TargetId = bot.Id,
            Details = details,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string? SanitizeNote(string? adminNote)
    {
        if (string.IsNullOrWhiteSpace(adminNote))
            return null;

        var trimmed = adminNote.Trim();
        return trimmed.Length <= MaxNoteLength ? trimmed : trimmed[..MaxNoteLength];
    }
}
