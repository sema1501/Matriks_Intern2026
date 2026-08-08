using System.Net;
using System.Text;
using CryptoTracker.API.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoTracker.API.Tests;

public class BinanceKlineHistoricalPaginationTests
{
    [Fact]
    public async Task GetHistoricalKlines_PaginatesChronologicallyWithoutDuplicates()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var page1 = BuildKlineJson(start, count: 1000, startPrice: 100m);
        var page2 = BuildKlineJson(start.AddMinutes(1000), count: 50, startPrice: 1100m);

        var handler = new SequencedHttpHandler(page1, page2);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.binance.com/")
        };

        var service = new BinanceKlineService(
            httpClient,
            NullLogger<BinanceKlineService>.Instance);

        var end = start.AddMinutes(1049);
        var candles = await service.GetHistoricalKlinesAsync(
            "BTCUSDT",
            "1m",
            start,
            end);

        Assert.Equal(1050, candles.Count);

        // Chronological and unique open times
        for (var i = 1; i < candles.Count; i++)
        {
            Assert.True(candles[i].OpenTimeUtc > candles[i - 1].OpenTimeUtc);
        }

        Assert.Equal(start, candles[0].OpenTimeUtc);
        Assert.Equal(start.AddMinutes(1049), candles[^1].OpenTimeUtc);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GetHistoricalKlines_StopsAtEndDate()
    {
        var start = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(9);
        // API returns more than requested end; service must filter.
        var payload = BuildKlineJson(start, count: 20, startPrice: 50m);

        var handler = new SequencedHttpHandler(payload);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.binance.com/")
        };

        var service = new BinanceKlineService(
            httpClient,
            NullLogger<BinanceKlineService>.Instance);

        var candles = await service.GetHistoricalKlinesAsync(
            "ETHUSDT",
            "1m",
            start,
            end);

        Assert.Equal(10, candles.Count);
        Assert.All(candles, c => Assert.True(c.OpenTimeUtc <= end));
    }

    [Fact]
    public async Task GetHistoricalKlines_ServerError_Throws()
    {
        var handler = new SequencedHttpHandler(
            (HttpStatusCode.ServiceUnavailable, "[]"));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.binance.com/")
        };

        var service = new BinanceKlineService(
            httpClient,
            NullLogger<BinanceKlineService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetHistoricalKlinesAsync(
                "BTCUSDT",
                "1m",
                DateTime.UtcNow.AddHours(-2),
                DateTime.UtcNow.AddHours(-1)));
    }

    [Fact]
    public async Task GetHistoricalKlines_ExceedsPageCap_ThrowsWithoutReturningPartialSuccess()
    {
        // Cap at 2 pages. Each page is full (1000), so a 3rd page would be required for the range.
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(2500); // needs > 2 full pages

        var page1 = BuildKlineJson(start, count: 1000, startPrice: 100m);
        var page2 = BuildKlineJson(start.AddMinutes(1000), count: 1000, startPrice: 1100m);
        // A 3rd page payload exists but must never be consumed successfully.
        var page3 = BuildKlineJson(start.AddMinutes(2000), count: 1000, startPrice: 2100m);

        var handler = new SequencedHttpHandler(page1, page2, page3);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.binance.com/")
        };

        var service = new BinanceKlineService(
            httpClient,
            NullLogger<BinanceKlineService>.Instance)
        {
            MaxPaginationPages = 2
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetHistoricalKlinesAsync("BTCUSDT", "1m", start, end));

        Assert.Contains("pagination limit", ex.Message, StringComparison.OrdinalIgnoreCase);
        // Only the allowed pages were requested; no successful partial return.
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GetClosingPricesAsync_ExcludesIncompleteFormingCandle()
    {
        var start = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        // 16 closed + 1 forming (close time in the future)
        var payload = BuildKlineJsonWithCloseTimes(
            start,
            closedCount: 16,
            includeFormingCandle: true,
            startPrice: 100m);

        var handler = new SequencedHttpHandler(payload);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.binance.com/")
        };

        var service = new BinanceKlineService(
            httpClient,
            NullLogger<BinanceKlineService>.Instance);

        var closes = await service.GetClosingPricesAsync("BTCUSDT", "1m", limit: 17);

        Assert.Equal(16, closes.Count);
        Assert.Equal(100m + 15, closes[^1]);
    }

    private static string BuildKlineJson(DateTime startUtc, int count, decimal startPrice)
    {
        var sb = new StringBuilder("[");
        for (var i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(',');
            var openMs = new DateTimeOffset(startUtc.AddMinutes(i)).ToUnixTimeMilliseconds();
            var closeMs = new DateTimeOffset(startUtc.AddMinutes(i + 1).AddMilliseconds(-1))
                .ToUnixTimeMilliseconds();
            var close = (startPrice + i).ToString(System.Globalization.CultureInfo.InvariantCulture);
            sb.Append(
                $"[{openMs},\"{close}\",\"{close}\",\"{close}\",\"{close}\",\"0\",{closeMs},\"0\",\"0\",\"0\",\"0\",\"0\"]");
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string BuildKlineJsonWithCloseTimes(
        DateTime startUtc,
        int closedCount,
        bool includeFormingCandle,
        decimal startPrice)
    {
        var sb = new StringBuilder("[");
        for (var i = 0; i < closedCount; i++)
        {
            if (i > 0) sb.Append(',');
            var openMs = new DateTimeOffset(startUtc.AddMinutes(i)).ToUnixTimeMilliseconds();
            var closeMs = new DateTimeOffset(startUtc.AddMinutes(i + 1).AddMilliseconds(-1))
                .ToUnixTimeMilliseconds();
            var close = (startPrice + i).ToString(System.Globalization.CultureInfo.InvariantCulture);
            sb.Append(
                $"[{openMs},\"{close}\",\"{close}\",\"{close}\",\"{close}\",\"0\",{closeMs},\"0\",\"0\",\"0\",\"0\",\"0\"]");
        }

        if (includeFormingCandle)
        {
            if (closedCount > 0) sb.Append(',');
            var openMs = new DateTimeOffset(startUtc.AddMinutes(closedCount)).ToUnixTimeMilliseconds();
            var closeMs = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds();
            var close = (startPrice + closedCount).ToString(System.Globalization.CultureInfo.InvariantCulture);
            sb.Append(
                $"[{openMs},\"{close}\",\"{close}\",\"{close}\",\"{close}\",\"0\",{closeMs},\"0\",\"0\",\"0\",\"0\",\"0\"]");
        }

        sb.Append(']');
        return sb.ToString();
    }

    private sealed class SequencedHttpHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses;

        public int RequestCount { get; private set; }

        public SequencedHttpHandler(params string[] bodies)
            : this(bodies.Select(b => (HttpStatusCode.OK, b)).ToArray())
        {
        }

        public SequencedHttpHandler(params (HttpStatusCode Status, string Body)[] responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            var (status, body) = _responses.Count > 0
                ? _responses.Dequeue()
                : (HttpStatusCode.OK, "[]");

            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
