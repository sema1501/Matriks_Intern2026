using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
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

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("bots")]
    public async Task<ActionResult<List<AdminBotDto>>> GetBots()
    {
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
                b.CreatedAt
            ))
            .ToListAsync();

        return Ok(bots);
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