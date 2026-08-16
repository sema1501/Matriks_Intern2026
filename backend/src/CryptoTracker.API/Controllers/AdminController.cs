using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
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
    private const int OvertradeWindowMinutes = 15;
    private const int OvertradeSignalThreshold = 5;

    public AdminController(AppDbContext context)
    {
        _context = context;
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
    public async Task<IActionResult> KillBot(int botId)
    {
        var bot = await _context.TradingBots.FirstOrDefaultAsync(b => b.Id == botId);
        if (bot is null)
            return NotFound(new { message = "Bot bulunamadı." });

        if (!bot.IsActive)
            return Ok(new { botId = bot.Id, isActive = false, message = "Bot zaten durdurulmuş." });

        bot.IsActive = false;
        await _context.SaveChangesAsync();

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
}
