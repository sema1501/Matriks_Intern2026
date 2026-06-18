using CryptoTracker.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet("daily-new-users")]
    public async Task<IActionResult> DailyNewUsers()
    {
        var today = DateTime.UtcNow.Date;
        var count = await db.Users.CountAsync(u => u.CreatedAt >= today);
        return Ok(new { date = today.ToString("yyyy-MM-dd"), newUserCount = count });
    }

    // TODO — Gorev 5 Bonus
    // GET /api/Dashboard/weekly-new-users  -> son 7 gunun gunluk dagilimi
    // GET /api/Dashboard/stats             -> toplam kullanici + toplam rol sayisi
}
