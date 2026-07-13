using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CryptoTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WatchlistController(IWatchlistService watchlistService) : ControllerBase
{
    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Kullanıcının izleme listesini döner.</summary>
    [HttpGet]
    public async Task<IActionResult> GetWatchlist()
        => Ok(await watchlistService.GetByUserAsync(CurrentUserId));

    /// <summary>İzleme listesine coin ekler.</summary>
    [HttpPost("{symbol}")]
    public async Task<IActionResult> AddToWatchlist(string symbol)
    {
        var item = await watchlistService.AddAsync(CurrentUserId, symbol);
        return CreatedAtAction(nameof(GetWatchlist), item);
    }

    /// <summary>İzleme listesinden coin siler.</summary>
    [HttpDelete("{symbol}")]
    public async Task<IActionResult> RemoveFromWatchlist(string symbol)
    {
        await watchlistService.RemoveAsync(CurrentUserId, symbol);
        return NoContent();
    }
}
