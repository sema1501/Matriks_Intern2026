using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CryptoTracker.API.Services;

public interface IBinancePriceService
{
    Task<IReadOnlyDictionary<string, decimal>> GetPricesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken);
}

public class BinancePriceService(HttpClient httpClient, ILogger<BinancePriceService> logger) : IBinancePriceService
{
    private const int MaxSymbolsPerRequest = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyDictionary<string, decimal>> GetPricesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken)
    {
        var unique = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (unique.Count == 0)
            return new Dictionary<string, decimal>(StringComparer.Ordinal);

        var result = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var chunk in unique.Chunk(MaxSymbolsPerRequest))
        {
            var prices = await FetchChunkAsync(chunk, cancellationToken);
            foreach (var (symbol, price) in prices)
                result[symbol] = price;
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<string, decimal>> FetchChunkAsync(
        string[] symbols,
        CancellationToken cancellationToken)
    {
        var symbolsJson = JsonSerializer.Serialize(symbols);
        var url = $"api/v3/ticker/price?symbols={Uri.EscapeDataString(symbolsJson)}";

        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Binance price request failed with status {StatusCode} for {SymbolCount} symbols",
                    (int)response.StatusCode,
                    symbols.Length);
                return new Dictionary<string, decimal>(StringComparer.Ordinal);
            }

            var payload = await response.Content.ReadFromJsonAsync<List<BinanceTickerPrice>>(JsonOptions, cancellationToken);
            if (payload is null)
            {
                logger.LogWarning("Binance returned an empty or malformed price payload");
                return new Dictionary<string, decimal>(StringComparer.Ordinal);
            }

            var map = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var item in payload)
            {
                if (string.IsNullOrWhiteSpace(item.Symbol) || string.IsNullOrWhiteSpace(item.Price))
                    continue;

                if (decimal.TryParse(item.Price, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
                    map[item.Symbol.Trim().ToUpperInvariant()] = price;
                else
                    logger.LogWarning("Could not parse Binance price for symbol {Symbol}", item.Symbol);
            }

            return map;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Transient failure while fetching Binance prices for {SymbolCount} symbols", symbols.Length);
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }
    }

    private sealed class BinanceTickerPrice
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public string Price { get; set; } = string.Empty;
    }
}
