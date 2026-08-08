using CryptoTracker.API.Models;

namespace CryptoTracker.API.Services;

/// <summary>
/// Shared RSI strategy semantics used by live monitoring and historical backtesting.
/// Matches BotMonitorService: threshold state (not crossover), period 14, interval 1m.
/// </summary>
public static class RsiSignalEvaluator
{
    public const int Period = 14;
    public const string Interval = "1m";
    public const int LiveKlineLimit = 100;

    /// <summary>
    /// Live-monitor threshold check (current RSI only).
    /// </summary>
    public static BotSignalType? DetermineSignalType(
        decimal rsi,
        decimal buyRsiThreshold,
        decimal sellRsiThreshold)
    {
        if (rsi <= buyRsiThreshold)
            return BotSignalType.Buy;

        if (rsi >= sellRsiThreshold)
            return BotSignalType.Sell;

        return null;
    }

    /// <summary>
    /// Historical backtest zone-entry signal.
    /// Emits only when RSI enters a threshold zone (or on the first in-range RSI already in a zone).
    /// </summary>
    public static BotSignalType? DetermineZoneEntrySignal(
        decimal currentRsi,
        decimal? previousInRangeRsi,
        decimal buyRsiThreshold,
        decimal sellRsiThreshold)
    {
        var current = DetermineSignalType(currentRsi, buyRsiThreshold, sellRsiThreshold);
        if (current is null)
            return null;

        // First valid in-range RSI already in a zone → one deterministic initial signal.
        if (previousInRangeRsi is null)
            return current;

        if (current == BotSignalType.Buy)
        {
            // Enter BUY zone from outside (previous was above buy threshold).
            return previousInRangeRsi.Value > buyRsiThreshold
                ? BotSignalType.Buy
                : null;
        }

        // Enter SELL zone from outside (previous was below sell threshold).
        return previousInRangeRsi.Value < sellRsiThreshold
            ? BotSignalType.Sell
            : null;
    }
}
