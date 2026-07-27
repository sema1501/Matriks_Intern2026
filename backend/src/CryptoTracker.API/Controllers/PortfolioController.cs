using CryptoTracker.API.DTOs;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CryptoTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PortfolioController(IPortfolioService portfolioService) : ControllerBase
{
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var balance = await portfolioService.GetBalanceAsync(userId, cancellationToken);
        return Ok(new BalanceDto(balance));
    }

    [HttpGet("holdings")]
    public async Task<IActionResult> GetHoldings(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var holdings = await portfolioService.GetHoldingsAsync(userId, cancellationToken);
        return Ok(holdings);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var transactions = await portfolioService.GetTransactionHistoryAsync(userId, cancellationToken);
        return Ok(transactions);
    }

    [HttpGet("leaderboard")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLeaderboard(CancellationToken cancellationToken)
    {
        var leaderboard = await portfolioService.GetLeaderboardAsync(cancellationToken);
        return Ok(leaderboard);
    }

    [HttpPost("buy")]
    public async Task<IActionResult> Buy([FromBody] TradeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await portfolioService.BuyAsync(userId, request.Symbol, request.Quantity, request.Price, cancellationToken);
        return Ok(result);
    }

    [HttpPost("sell")]
    public async Task<IActionResult> Sell([FromBody] TradeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { error = "Geçersiz kullanıcı kimliği." });

        var result = await portfolioService.SellAsync(userId, request.Symbol, request.Quantity, request.Price, cancellationToken);
        return Ok(result);
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}