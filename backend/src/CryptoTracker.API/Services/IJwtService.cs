using CryptoTracker.API.Models;
namespace CryptoTracker.API.Services;
public interface IJwtService
{
    string GenerateToken(User user, IEnumerable<string> roles);
}
