using CryptoTracker.API.Services;
using Xunit;

namespace CryptoTracker.API.Tests;

public class RsiCalculatorTests
{
    [Fact]
    public void Calculate_WithKnownWilderExample_ReturnsExpectedRsi()
    {
        // Wilder'ın bilinen 14 periyotluk RSI örneği
        var closingPrices = new decimal[]
        {
            44.34m,
            44.09m,
            44.15m,
            43.61m,
            44.33m,
            44.83m,
            45.10m,
            45.42m,
            45.84m,
            46.08m,
            45.89m,
            46.03m,
            45.61m,
            46.28m,
            46.28m
        };

        var result = RsiCalculator.Calculate(closingPrices, 14);

        Assert.Equal(70.46m, result);
    }

    [Fact]
    public void CalculateSeries_MatchesCalculateAtEachBar()
    {
        var closingPrices = new decimal[]
        {
            44.34m,
            44.09m,
            44.15m,
            43.61m,
            44.33m,
            44.83m,
            45.10m,
            45.42m,
            45.84m,
            46.08m,
            45.89m,
            46.03m,
            45.61m,
            46.28m,
            46.28m,
            46.05m,
            46.50m
        };

        var series = RsiCalculator.CalculateSeries(closingPrices, 14);

        Assert.Equal(closingPrices.Length, series.Count);

        for (var i = 0; i < 14; i++)
            Assert.Null(series[i]);

        for (var i = 14; i < closingPrices.Length; i++)
        {
            var slice = closingPrices.Take(i + 1).ToList();
            var expected = RsiCalculator.Calculate(slice, 14);
            Assert.Equal(expected, series[i]);
        }
    }

    [Fact]
    public void CalculateSeries_InsufficientData_ReturnsAllNull()
    {
        var prices = Enumerable.Range(1, 10).Select(i => (decimal)i).ToList();
        var series = RsiCalculator.CalculateSeries(prices, 14);

        Assert.All(series, v => Assert.Null(v));
    }
}
