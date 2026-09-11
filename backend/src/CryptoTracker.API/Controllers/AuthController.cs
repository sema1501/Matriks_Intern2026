using CryptoTracker.API.DTOs;
using CryptoTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CryptoTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService, IUserService userService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        => Ok(new { message = await authService.RegisterAsync(request) });

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        await authService.ConfirmEmailAsync(request.Token);
        return Ok(new { message = "E-posta adresiniz doğrulandı. Artık giriş yapabilirsiniz." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
        => Ok(await authService.LoginAsync(request));

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    => Ok(new { message = await authService.ForgotPasswordAsync(request.Email) });

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        await authService.ResetPasswordAsync(request.Token, request.NewPassword);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await userService.GetByIdAsync(userId));
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await userService.UpdateProfileAsync(userId, request));
    }

    [Authorize]
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await userService.ChangePasswordAsync(userId, request);
        return NoContent();
    }

    [Authorize]
    [HttpPut("me/notifications")]
    public async Task<IActionResult> UpdateNotifications([FromBody] UpdateNotificationsRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await userService.SetEmailNotificationsAsync(userId, request.Enabled));
    }

    [Authorize]
    [HttpPut("me/avatar")]
    public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await userService.SetAvatarAsync(userId, request.AvatarUrl));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
        => Ok(await userService.GetAllAsync());
}
