using System.Text.Json;

namespace CryptoTracker.API.Services;

public interface IBinanceTestnetClient
{
    Task<JsonElement> GetAccountAsync();

    Task<JsonElement> CreateOrderAsync(
        string symbol,
        string side,
        string type,
        decimal? quantity = null,
        decimal? quoteOrderQty = null);
}