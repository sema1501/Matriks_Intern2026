using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class TestnetController : ControllerBase
{
    private readonly BinanceTestnetClient _binanceTestnetClient;

    public TestnetController(BinanceTestnetClient binanceTestnetClient)
    {
        _binanceTestnetClient = binanceTestnetClient;
    }

    [HttpGet("account")]
    public async Task<IActionResult> GetAccount()
    {
        try
        {
            var account = await _binanceTestnetClient.GetAccountAsync();
            return Ok(account);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = ex.Message
            });
        }
    }
}