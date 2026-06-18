using CryptoTracker.API.DTOs;
namespace CryptoTracker.API.Services;
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
}
