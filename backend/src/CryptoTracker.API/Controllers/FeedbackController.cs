using System.Security.Claims;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedbackController(IFeedbackService feedbackService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetAll()
    {
        var feedbacks = await feedbackService.GetAllAsync();
        return Ok(feedbacks);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(CreateFeedbackDto request)
    {
        int? userId = null;

        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (int.TryParse(userIdClaim, out var id))
            {
                userId = id;
            }
        }

        await feedbackService.CreateAsync(request, userId);

        return Ok(new
        {
            message = "Geri bildiriminiz başarıyla gönderildi."
        });
    }
}