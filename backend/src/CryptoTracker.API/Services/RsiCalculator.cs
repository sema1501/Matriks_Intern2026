namespace CryptoTracker.API.Services;

public static class RsiCalculator
{
    /// <summary>
    /// Wilder RSI for the final closing price in the series.
    /// Requires at least <paramref name="period"/> + 1 closing prices.
    /// </summary>
    public static decimal Calculate(
        IReadOnlyList<decimal> closingPrices,
        int period = 14)
    {
        var series = CalculateSeries(closingPrices, period);
        var last = series[^1];

        if (last is null)
        {
            throw new ArgumentException(
                $"RSI hesaplamak için en az {period + 1} kapanış fiyatı gereklidir.",
                nameof(closingPrices));
        }

        return last.Value;
    }

    /// <summary>
    /// Per-bar Wilder RSI. Index i is null until enough warm-up bars exist
    /// (first value appears at index <paramref name="period"/>).
    /// Each value is rounded to 2 decimal places, matching <see cref="Calculate"/>.
    /// </summary>
    public static IReadOnlyList<decimal?> CalculateSeries(
        IReadOnlyList<decimal> closingPrices,
        int period = 14)
    {
        if (closingPrices is null)
            throw new ArgumentNullException(nameof(closingPrices));

        if (period <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(period),
                "RSI periyodu sıfırdan büyük olmalıdır.");

        var result = new decimal?[closingPrices.Count];

        if (closingPrices.Count < period + 1)
            return result;

        decimal totalGain = 0;
        decimal totalLoss = 0;

        for (var i = 1; i <= period; i++)
        {
            var difference = closingPrices[i] - closingPrices[i - 1];

            if (difference > 0)
                totalGain += difference;
            else
                totalLoss += Math.Abs(difference);
        }

        var averageGain = totalGain / period;
        var averageLoss = totalLoss / period;

        result[period] = ToRsi(averageGain, averageLoss);

        for (var i = period + 1; i < closingPrices.Count; i++)
        {
            var difference = closingPrices[i] - closingPrices[i - 1];
            var gain = difference > 0 ? difference : 0;
            var loss = difference < 0 ? Math.Abs(difference) : 0;

            averageGain =
                ((averageGain * (period - 1)) + gain) / period;

            averageLoss =
                ((averageLoss * (period - 1)) + loss) / period;

            result[i] = ToRsi(averageGain, averageLoss);
        }

        return result;
    }

    private static decimal ToRsi(decimal averageGain, decimal averageLoss)
    {
        if (averageLoss == 0)
            return averageGain == 0 ? 50m : 100m;

        if (averageGain == 0)
            return 0m;

        var relativeStrength = averageGain / averageLoss;
        var rsi = 100m - (100m / (1m + relativeStrength));

        return Math.Round(rsi, 2);
    }
}
