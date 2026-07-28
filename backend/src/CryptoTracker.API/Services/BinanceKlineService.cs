using System.Globalization;
using System.Text.Json;

namespace CryptoTracker.API.Services;

public interface IBinanceKlineService
{
    Task<IReadOnlyList<decimal>> GetClosingPricesAsync(
        string symbol,
        string interval = "1m",
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public sealed class BinanceKlineService(
    HttpClient httpClient,
    ILogger<BinanceKlineService> logger) : IBinanceKlineService
{
    public async Task<IReadOnlyList<decimal>> GetClosingPricesAsync(
        string symbol,
        string interval = "1m",
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException(
                "Sembol boş olamaz.",
                nameof(symbol));

        if (limit is < 15 or > 1000)
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                "Kline limiti 15 ile 1000 arasında olmalıdır.");

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        var url =
            $"api/v3/klines?symbol={Uri.EscapeDataString(normalizedSymbol)}" +
            $"&interval={Uri.EscapeDataString(interval)}&limit={limit}";

        try
        {
            using var response =
                await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Binance kline request failed with status {StatusCode} for symbol {Symbol}",
                    (int)response.StatusCode,
                    normalizedSymbol);

                return Array.Empty<decimal>();
            }

            await using var stream =
                await response.Content.ReadAsStreamAsync(cancellationToken);

            using var document =
                await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                logger.LogWarning(
                    "Binance returned an invalid kline payload for symbol {Symbol}",
                    normalizedSymbol);

                return Array.Empty<decimal>();
            }

            var closingPrices = new List<decimal>();

            foreach (var kline in document.RootElement.EnumerateArray())
            {
                // Binance kline dizisinde 4. indeks kapanış fiyatıdır.
                if (kline.ValueKind != JsonValueKind.Array ||
                    kline.GetArrayLength() <= 4)
                {
                    continue;
                }

                var closeText = kline[4].GetString();

                if (decimal.TryParse(
                        closeText,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var closePrice))
                {
                    closingPrices.Add(closePrice);
                }
                else
                {
                    logger.LogWarning(
                        "Could not parse Binance closing price for symbol {Symbol}",
                        normalizedSymbol);
                }
            }

            return closingPrices;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Transient failure while fetching Binance klines for symbol {Symbol}",
                normalizedSymbol);

            return Array.Empty<decimal>();
        }
    }
}