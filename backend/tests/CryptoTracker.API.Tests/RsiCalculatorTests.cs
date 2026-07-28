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
}