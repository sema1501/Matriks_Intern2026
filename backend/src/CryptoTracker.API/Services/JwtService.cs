using CryptoTracker.API.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CryptoTracker.API.Services;

public class JwtService(IConfiguration config) : IJwtService
{
    public const int DefaultAccessTokenMinutes = 15;

    public string GenerateToken(User user, IEnumerable<string> roles)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name,  user.Username),
            new(ClaimTypes.Email, user.Email)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var accessMinutes = config.GetValue("Jwt:AccessTokenMinutes", DefaultAccessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer:   config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims:   claims,
            expires:  DateTime.UtcNow.AddMinutes(accessMinutes),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// 64 bayt kriptografik olarak güvenli rastgele değer üretir (tahmin edilemez).
    /// Base64Url biçimi URL ve JSON içinde kaçış gerektirmez.
    /// </summary>
    public string GenerateRefreshToken()
        => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    /// <summary>Veritabanına yazılacak/aranacak SHA-256 hash (hex).</summary>
    public static string HashRefreshToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
