namespace CryptoTracker.API.DTOs;
public record RegisterRequest(string Username, string Email, string Password);
public record LoginRequest(string UsernameOrEmail, string Password);
public record AuthResponse(string Token, string Username, IEnumerable<string> Roles);
public record UpdateProfileRequest(string? Username, string? Email);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
