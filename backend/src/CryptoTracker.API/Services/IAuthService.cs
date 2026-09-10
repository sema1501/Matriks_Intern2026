using CryptoTracker.API.DTOs;
namespace CryptoTracker.API.Services;
public interface IAuthService
{
    Task<string> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<string> ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(string token, string newPassword);
    Task ConfirmEmailAsync(string token);
}
