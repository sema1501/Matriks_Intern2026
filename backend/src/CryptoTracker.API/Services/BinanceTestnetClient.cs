using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CryptoTracker.API.Services;

public class BinanceTestnetClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _apiSecret;

    public BinanceTestnetClient(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;

        var baseUrl = configuration["BinanceTestnet:BaseUrl"]
            ?? "https://testnet.binance.vision";

        _apiKey = configuration["BinanceTestnet:ApiKey"] ?? string.Empty;
        _apiSecret = configuration["BinanceTestnet:ApiSecret"] ?? string.Empty;

        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    private string CreateSignature(string queryString)
    {
        using var hmac =
            new HMACSHA256(Encoding.UTF8.GetBytes(_apiSecret));

        var hash =
            hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    public async Task<JsonElement> GetAccountAsync()
{
    if (string.IsNullOrWhiteSpace(_apiKey) ||
        string.IsNullOrWhiteSpace(_apiSecret))
    {
        throw new InvalidOperationException(
            "Binance Testnet API key veya secret yapılandırılmamış.");
    }

    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    var queryString = $"timestamp={timestamp}&recvWindow=5000";
    var signature = CreateSignature(queryString);

    using var request = new HttpRequestMessage(
        HttpMethod.Get,
        $"/api/v3/account?{queryString}&signature={signature}");

    request.Headers.Add("X-MBX-APIKEY", _apiKey);

    using var response = await _httpClient.SendAsync(request);
    var responseBody = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(
            $"Binance Testnet isteği başarısız: " +
            $"{(int)response.StatusCode} - {responseBody}");
    }

    return JsonSerializer.Deserialize<JsonElement>(responseBody);
}
}