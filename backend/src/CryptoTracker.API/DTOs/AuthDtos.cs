namespace CryptoTracker.API.DTOs;
public record RegisterRequest(string Username, string Email, string Password);
public record LoginRequest(string UsernameOrEmail, string Password);
// İstemciye dönen yanıt: refresh token burada yok, yalnızca HttpOnly cookie ile gider.
public record AuthResponse(string Token, string Username, IEnumerable<string> Roles);
// Servisin controller'a verdiği sonuç, RefreshToken cookie'ye yazılmak içindir.
public record AuthSession(string Token, string RefreshToken, string Username, IEnumerable<string> Roles)
{
    public AuthResponse ToResponse() => new(Token, Username, Roles);
}
public record UpdateProfileRequest(string? Username, string? Email);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token,string NewPassword);
public record ConfirmEmailRequest(string Token);
public record UpdateNotificationsRequest(bool Enabled);
public record UpdateAvatarRequest(string? AvatarUrl);