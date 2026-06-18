namespace CryptoTracker.API.DTOs;
public record UserDto(int Id, string Username, string Email, IEnumerable<string> Roles, DateTime CreatedAt);
