using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CryptoTracker.API.Services;

public class BinanceTestnetClient : IBinanceTestnetClient
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

        _apiKey = configuration["BinanceTestnet:ApiKey"]?.Trim() ?? string.Empty;
        _apiSecret = configuration["BinanceTestnet:ApiSecret"]?.Trim() ?? string.Empty;

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

    private void EnsureCredentialsConfigured()
    {
        if (string.IsNullOrWhiteSpace(_apiKey) ||
            string.IsNullOrWhiteSpace(_apiSecret))
        {
            throw new InvalidOperationException(
                "Binance Testnet API key veya secret yapılandırılmamış.");
        }
    }

    public async Task<JsonElement> GetAccountAsync()
    {
        EnsureCredentialsConfigured();

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var queryString = $"timestamp={timestamp}&recvWindow=60000";
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

    public async Task<JsonElement> CreateOrderAsync(
        string symbol,
        string side,
        string type,
        decimal? quantity = null,
        decimal? quoteOrderQty = null)
    {
        EnsureCredentialsConfigured();

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var queryParams = new List<string>
        {
            $"symbol={symbol.ToUpperInvariant()}",
            $"side={side.ToUpperInvariant()}",
            $"type={type.ToUpperInvariant()}",
            $"timestamp={timestamp}",
            "recvWindow=60000"
        };

        if (quantity.HasValue)
        {
            queryParams.Add(
                $"quantity={quantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        if (quoteOrderQty.HasValue)
        {
            queryParams.Add(
                $"quoteOrderQty={quoteOrderQty.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        var queryString = string.Join("&", queryParams);
        var signature = CreateSignature(queryString);
        var requestUrl = $"/api/v3/order?{queryString}&signature={signature}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Add("X-MBX-APIKEY", _apiKey);

        using var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Binance Testnet emir gönderme isteği başarısız: " +
                $"{(int)response.StatusCode} - {responseBody}");
        }

        return JsonSerializer.Deserialize<JsonElement>(responseBody);
    }
}