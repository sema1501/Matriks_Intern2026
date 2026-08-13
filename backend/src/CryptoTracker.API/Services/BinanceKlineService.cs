using System.Globalization;
using System.Text.Json;

namespace CryptoTracker.API.Services;

public sealed record BinanceKlineCandle(
    DateTime OpenTimeUtc,
    decimal ClosePrice,
    DateTime? CloseTimeUtc = null);

public interface IBinanceKlineService
{
    Task<IReadOnlyList<decimal>> GetClosingPricesAsync(
        string symbol,
        string interval = "1m",
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches public historical klines for [startTimeUtc, endTimeUtc] (inclusive by open time),
    /// paginating past Binance's 1000-candle page limit. Chronological, de-duplicated.
    /// </summary>
    Task<IReadOnlyList<BinanceKlineCandle>> GetHistoricalKlinesAsync(
        string symbol,
        string interval,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        CancellationToken cancellationToken = default);
}

public sealed class BinanceKlineService : IBinanceKlineService
{
    public const int DefaultMaxPaginationPages = 500;
    private const int MaxCandlesPerRequest = 1000;

    private readonly HttpClient _httpClient;
    private readonly ILogger<BinanceKlineService> _logger;

    /// <summary>
    /// Defensive pagination cap. Overridable in tests via InternalsVisibleTo.
    /// </summary>
    internal int MaxPaginationPages { get; set; } = DefaultMaxPaginationPages;

    public BinanceKlineService(
        HttpClient httpClient,
        ILogger<BinanceKlineService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

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
            var page = await FetchKlinePageCoreAsync(
                url,
                normalizedSymbol,
                throwOnFailure: false,
                cancellationToken);

            // Binance includes the currently forming candle as the last row.
            // Live zone-entry must compare consecutive CLOSED bars only; otherwise an
            // entry on the newly closed bar is shadowed (previous already in-zone) and
            // missed until RSI exits and re-enters on the open candle.
            var nowUtc = DateTime.UtcNow;
            return page
                .Where(c => c.CloseTimeUtc is not { } closeTime || closeTime <= nowUtc)
                .Select(c => c.ClosePrice)
                .ToList();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Transient failure while fetching Binance klines for symbol {Symbol}",
                normalizedSymbol);

            return Array.Empty<decimal>();
        }
    }

    public async Task<IReadOnlyList<BinanceKlineCandle>> GetHistoricalKlinesAsync(
        string symbol,
        string interval,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException(
                "Sembol boş olamaz.",
                nameof(symbol));

        if (string.IsNullOrWhiteSpace(interval))
            throw new ArgumentException(
                "Interval boş olamaz.",
                nameof(interval));

        if (startTimeUtc >= endTimeUtc)
            throw new ArgumentException(
                "Başlangıç tarihi bitiş tarihinden önce olmalıdır.");

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var startMs = ToUnixMilliseconds(startTimeUtc);
        var endMs = ToUnixMilliseconds(endTimeUtc);

        var results = new List<BinanceKlineCandle>();
        var seenOpenTimes = new HashSet<long>();
        var cursorMs = startMs;
        var pages = 0;

        while (cursorMs <= endMs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (++pages > MaxPaginationPages)
            {
                _logger.LogWarning(
                    "Historical kline pagination exceeded {MaxPages} pages for {Symbol}",
                    MaxPaginationPages,
                    normalizedSymbol);

                throw new InvalidOperationException(
                    "Requested historical range exceeds the supported pagination limit. Narrow the backtest date range.");
            }

            var url =
                $"api/v3/klines?symbol={Uri.EscapeDataString(normalizedSymbol)}" +
                $"&interval={Uri.EscapeDataString(interval)}" +
                $"&startTime={cursorMs}&endTime={endMs}&limit={MaxCandlesPerRequest}";

            var page = await FetchKlinePageCoreAsync(
                url,
                normalizedSymbol,
                throwOnFailure: true,
                cancellationToken);

            if (page.Count == 0)
                break;

            long lastOpenMs = cursorMs;

            foreach (var candle in page)
            {
                var openMs = ToUnixMilliseconds(candle.OpenTimeUtc);

                if (openMs < startMs || openMs > endMs)
                    continue;

                if (!seenOpenTimes.Add(openMs))
                    continue;

                results.Add(candle);
                lastOpenMs = openMs;
            }

            if (page.Count < MaxCandlesPerRequest)
                break;

            var nextCursor = lastOpenMs + 1;
            if (nextCursor <= cursorMs)
                break;

            cursorMs = nextCursor;
        }

        return results;
    }

    private async Task<IReadOnlyList<BinanceKlineCandle>> FetchKlinePageCoreAsync(
        string url,
        string normalizedSymbol,
        bool throwOnFailure,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response =
                await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Binance kline request failed with status {StatusCode} for symbol {Symbol}",
                    (int)response.StatusCode,
                    normalizedSymbol);

                if (throwOnFailure)
                {
                    throw new InvalidOperationException(
                        "Binance piyasa verisi şu anda kullanılamıyor.");
                }

                return Array.Empty<BinanceKlineCandle>();
            }

            await using var stream =
                await response.Content.ReadAsStreamAsync(cancellationToken);

            using var document =
                await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning(
                    "Binance returned an invalid kline payload for symbol {Symbol}",
                    normalizedSymbol);

                if (throwOnFailure)
                {
                    throw new InvalidOperationException(
                        "Binance geçersiz bir kline yanıtı döndürdü.");
                }

                return Array.Empty<BinanceKlineCandle>();
            }

            var candles = new List<BinanceKlineCandle>();

            foreach (var kline in document.RootElement.EnumerateArray())
            {
                if (kline.ValueKind != JsonValueKind.Array ||
                    kline.GetArrayLength() <= 4)
                {
                    continue;
                }

                long openTimeMs;
                long? closeTimeMs = null;
                try
                {
                    openTimeMs = kline[0].GetInt64();
                    if (kline.GetArrayLength() > 6)
                        closeTimeMs = kline[6].GetInt64();
                }
                catch (Exception)
                {
                    _logger.LogWarning(
                        "Could not parse Binance kline open/close time for symbol {Symbol}",
                        normalizedSymbol);
                    continue;
                }

                var closeText = kline[4].GetString();

                if (!decimal.TryParse(
                        closeText,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var closePrice))
                {
                    _logger.LogWarning(
                        "Could not parse Binance closing price for symbol {Symbol}",
                        normalizedSymbol);
                    continue;
                }

                DateTime? closeTimeUtc = closeTimeMs is { } ms
                    ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
                    : null;

                candles.Add(new BinanceKlineCandle(
                    DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs).UtcDateTime,
                    closePrice,
                    closeTimeUtc));
            }

            return candles;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException) when (throwOnFailure)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Transient failure while fetching Binance klines for symbol {Symbol}",
                normalizedSymbol);

            if (throwOnFailure)
            {
                throw new InvalidOperationException(
                    "Binance piyasa verisi alınırken bir hata oluştu.",
                    ex);
            }

            return Array.Empty<BinanceKlineCandle>();
        }
    }

    private static long ToUnixMilliseconds(DateTime utc)
    {
        var dto = utc.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(utc),
            DateTimeKind.Local => new DateTimeOffset(utc.ToUniversalTime()),
            _ => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc))
        };

        return dto.ToUnixTimeMilliseconds();
    }
}
