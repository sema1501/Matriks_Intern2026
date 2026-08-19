using CryptoTracker.API.Models;

namespace CryptoTracker.API.Services;

/// <summary>
/// Turns two EMA series into a trade signal.
/// A crossover is an EVENT (the moment the relationship flips), not a STATE
/// (short staying above long). Emitting on state would fire on every candle
/// of a trend and generate dozens of redundant orders.
/// </summary>
public static class EmaCrossoverEvaluator
{
    public const string Interval = RsiSignalEvaluator.Interval;

    public const int SeedWarmupCandles = 50;

    public static int RequiredCandleCount(int longPeriod) => longPeriod + 1;

    public static BotSignalType? DetermineCrossoverSignal(
        decimal? previousShort,
        decimal? previousLong,
        decimal? currentShort,
        decimal? currentLong)
    {
        if (previousShort is null || previousLong is null ||
            currentShort is null || currentLong is null)
            return null;

        var previousDifference = previousShort.Value - previousLong.Value;
        var currentDifference = currentShort.Value - currentLong.Value;

        if (previousDifference <= 0m && currentDifference > 0m)
            return BotSignalType.Buy;

        if (previousDifference >= 0m && currentDifference < 0m)
            return BotSignalType.Sell;

        return null;
    }
}