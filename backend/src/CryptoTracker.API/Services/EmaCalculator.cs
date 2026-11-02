namespace CryptoTracker.API.Services;

/// <summary>
/// Exponential Moving Average (EMA) calculation.
/// Pure function: no database, no HTTP, no clock — fully unit-testable.
/// </summary>
public static class EmaCalculator
{
    /// <summary>
    /// Calculates the EMA series for the given closing prices.
    /// The returned list has the SAME length as the input; warm-up positions are null.
    /// This keeps two series with different periods index-aligned, which is required
    /// for crossover detection.
    /// </summary>
    public static IReadOnlyList<decimal?> CalculateSeries(
        IReadOnlyList<decimal> closingPrices,
        int period)
    {
        ArgumentNullException.ThrowIfNull(closingPrices);

        if (period < 1)
            throw new ArgumentOutOfRangeException(
                nameof(period), "EMA period must be at least 1.");

        var result = new decimal?[closingPrices.Count];

        if (closingPrices.Count < period)
            return result; // not enough data — every position stays null

        // Smoothing factor. The 'm' suffix is required: 2 / (period + 1) would be
        // integer division and would evaluate to 0 for every period greater than 1.
        var multiplier = 2m / (period + 1);

        // Seed the series with the simple average of the first 'period' prices.
        // This is the standard convention used by TradingView and Binance.
        decimal sum = 0m;
        for (var i = 0; i < period; i++)
            sum += closingPrices[i];

        var ema = sum / period;
        result[period - 1] = ema;

        for (var i = period; i < closingPrices.Count; i++)
        {
            // No intermediate rounding: rounding here would accumulate error
            // across the chain and could flip a crossover comparison.
            ema = closingPrices[i] * multiplier + ema * (1m - multiplier);
            result[i] = ema;
        }

        return result;
    }
}