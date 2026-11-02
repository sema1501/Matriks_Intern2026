using CryptoTracker.API.Models;
using CryptoTracker.API.Services;

namespace CryptoTracker.API.Tests;

public class EmaCalculatorTests
{
    // Hand-verified reference series (Görev 39 documentation).
    // Downtrend followed by an uptrend, producing a golden cross at index 8.
    private static readonly decimal[] GoldenCrossPrices =
        [110, 108, 106, 104, 102, 100, 101, 103, 106, 109, 112, 115];

    private const int ShortPeriod = 3;
    private const int LongPeriod = 5;

    [Fact]
    public void CalculateSeries_ReturnsSameLengthAsInput()
    {
        var ema = EmaCalculator.CalculateSeries(GoldenCrossPrices, LongPeriod);

        Assert.Equal(GoldenCrossPrices.Length, ema.Count);
    }

    [Fact]
    public void CalculateSeries_LeavesWarmUpRegionNull()
    {
        var ema = EmaCalculator.CalculateSeries(GoldenCrossPrices, LongPeriod);

        Assert.All(ema.Take(LongPeriod - 1), value => Assert.Null(value));
        Assert.NotNull(ema[LongPeriod - 1]);
    }

    [Fact]
    public void CalculateSeries_SeedsWithSimpleMovingAverage()
    {
        // (110 + 108 + 106) / 3 = 108
        Assert.Equal(108m, EmaCalculator.CalculateSeries(GoldenCrossPrices, 3)[2]);

        // (110 + 108 + 106 + 104 + 102) / 5 = 106
        Assert.Equal(106m, EmaCalculator.CalculateSeries(GoldenCrossPrices, 5)[4]);
    }

    [Fact] // GOLDEN TEST — every value below was verified by hand.
    public void CalculateSeries_MatchesHandVerifiedValues()
    {
        var shortEma = EmaCalculator.CalculateSeries(GoldenCrossPrices, ShortPeriod);
        var longEma = EmaCalculator.CalculateSeries(GoldenCrossPrices, LongPeriod);

        // Candle before the cross: short is still below long.
        Assert.Equal(102.250m, decimal.Round(shortEma[7]!.Value, 3));
        Assert.Equal(103.000m, decimal.Round(longEma[7]!.Value, 3));

        // Cross candle:
        //   short: 106 * 0.5    + 102.25 * 0.5    = 104.125
        //   long:  106 * (1/3)  + 103    * (2/3)  = 104.000
        Assert.Equal(104.125m, decimal.Round(shortEma[8]!.Value, 3));
        Assert.Equal(104.000m, decimal.Round(longEma[8]!.Value, 3));

        // Candle after the cross.
        Assert.Equal(106.562500m, decimal.Round(shortEma[9]!.Value, 6));
        Assert.Equal(105.666667m, decimal.Round(longEma[9]!.Value, 6));
    }

    [Fact]
    public void CalculateSeries_WithPeriodOne_ReturnsPricesUnchanged()
    {
        // multiplier = 2 / 2 = 1, so every EMA equals its own closing price.
        var ema = EmaCalculator.CalculateSeries([5m, 7m, 9m], 1);

        Assert.Equal(new decimal?[] { 5m, 7m, 9m }, ema);
    }

    [Fact]
    public void CalculateSeries_WithInsufficientData_ReturnsAllNull()
    {
        var ema = EmaCalculator.CalculateSeries([1m, 2m], 5);

        Assert.All(ema, Assert.Null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CalculateSeries_WithInvalidPeriod_Throws(int period)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EmaCalculator.CalculateSeries(GoldenCrossPrices, period));
    }
}

public class EmaCrossoverEvaluatorTests
{
    // Golden cross at index 8.
    private static readonly decimal[] GoldenCrossPrices =
        [110, 108, 106, 104, 102, 100, 101, 103, 106, 109, 112, 115];

    // Mirror of the series above: death cross at the same index.
    private static readonly decimal[] DeathCrossPrices =
        [100, 102, 104, 106, 108, 110, 109, 107, 104, 101, 98, 95];

    private const int ShortPeriod = 3;
    private const int LongPeriod = 5;

    [Fact]
    public void DetermineCrossoverSignal_OnGoldenCross_ReturnsBuy()
    {
        var (shortEma, longEma) = Calculate(GoldenCrossPrices);

        var signal = EmaCrossoverEvaluator.DetermineCrossoverSignal(
            shortEma[7], longEma[7], shortEma[8], longEma[8]);

        Assert.Equal(BotSignalType.Buy, signal);
    }

    [Fact]
    public void DetermineCrossoverSignal_OnDeathCross_ReturnsSell()
    {
        var (shortEma, longEma) = Calculate(DeathCrossPrices);

        var signal = EmaCrossoverEvaluator.DetermineCrossoverSignal(
            shortEma[7], longEma[7], shortEma[8], longEma[8]);

        Assert.Equal(BotSignalType.Sell, signal);
    }

    [Fact]
    public void DetermineCrossoverSignal_WhileShortStaysAbove_ReturnsNull()
    {
        // The cross already happened at index 8. Short remaining above long is a
        // STATE, not an event — no further signal may be emitted.
        var (shortEma, longEma) = Calculate(GoldenCrossPrices);

        var signal = EmaCrossoverEvaluator.DetermineCrossoverSignal(
            shortEma[8], longEma[8], shortEma[9], longEma[9]);

        Assert.Null(signal);
    }

    [Fact]
    public void DetermineCrossoverSignal_InsideWarmUpRegion_ReturnsNull()
    {
        var (shortEma, longEma) = Calculate(GoldenCrossPrices);

        // longEma[3] is null — the long EMA has not started yet.
        var signal = EmaCrossoverEvaluator.DetermineCrossoverSignal(
            shortEma[3], longEma[3], shortEma[4], longEma[4]);

        Assert.Null(signal);
    }

    [Fact]
    public void RequiredCandleCount_IsLongPeriodPlusOne()
    {
        // Two consecutive EMA points are needed to detect a crossover.
        Assert.Equal(22, EmaCrossoverEvaluator.RequiredCandleCount(21));
    }

    private static (IReadOnlyList<decimal?> Short, IReadOnlyList<decimal?> Long) Calculate(
        decimal[] prices)
        => (EmaCalculator.CalculateSeries(prices, ShortPeriod),
            EmaCalculator.CalculateSeries(prices, LongPeriod));
}