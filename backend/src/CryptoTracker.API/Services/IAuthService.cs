using CryptoTracker.API.DTOs;
namespace CryptoTracker.API.Services;
public interface IAuthService
{
    Task<string> RegisterAsync(RegisterRequest request);
    Task<AuthSession> LoginAsync(LoginRequest request);
    Task<AuthSession> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task<string> ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(string token, string newPassword);
    Task ConfirmEmailAsync(string token);
}
