using CryptoTracker.API.Models;

namespace CryptoTracker.API.Services;

public static class AlertConditionEvaluator
{
    /// <summary>
    /// Returns true when the alert's condition is satisfied for the given current price.
    /// Handles both fixed-price alerts and percent-change alerts (Görev 46).
    /// Unknown directions/types return false.
    /// </summary>
    public static bool IsConditionSatisfied(PriceAlert alert, decimal currentPrice)
    {
        return alert.Type switch
        {
            AlertType.Price => IsPriceConditionSatisfied(alert, currentPrice),
            AlertType.PercentChange => IsPercentChangeConditionSatisfied(alert, currentPrice),
            _ => false
        };
    }

    private static bool IsPriceConditionSatisfied(PriceAlert alert, decimal currentPrice)
    {
        return alert.Direction switch
        {
            AlertDirection.Above => currentPrice >= alert.TargetPrice,
            AlertDirection.Below => currentPrice <= alert.TargetPrice,
            _ => false
        };
    }

    /// <summary>
    /// Percent-change alert: compares the current price against the reference price captured
    /// when the alert was created. Above = rose by at least the threshold; Below = fell by at least it.
    /// Missing reference/threshold or non-positive reference returns false (cannot evaluate safely).
    /// </summary>
    private static bool IsPercentChangeConditionSatisfied(PriceAlert alert, decimal currentPrice)
    {
        if (alert.ReferencePrice is not { } reference || reference <= 0m)
            return false;

        if (alert.PercentChangeThreshold is not { } threshold || threshold <= 0m)
            return false;

        var changePercent = (currentPrice - reference) / reference * 100m;

        return alert.Direction switch
        {
            AlertDirection.Above => changePercent >= threshold,
            AlertDirection.Below => changePercent <= -threshold,
            _ => false
        };
    }

    /// <summary>
    /// Maps a known interval to its evaluation cadence.
    /// Returns false for unknown/future enum values so callers can skip without crashing.
    /// </summary>
    public static bool TryGetCadence(AlertInterval interval, out TimeSpan cadence)
    {
        switch (interval)
        {
            case AlertInterval.Minute:
                cadence = TimeSpan.FromMinutes(1);
                return true;
            case AlertInterval.Hourly:
                cadence = TimeSpan.FromHours(1);
                return true;
            case AlertInterval.Daily:
                cadence = TimeSpan.FromDays(1);
                return true;
            default:
                cadence = default;
                return false;
        }
    }

    public static bool IsIntervalSupported(AlertInterval interval) =>
        TryGetCadence(interval, out _);

    /// <summary>
    /// Option A: null LastCheckedAt is immediately due; otherwise due when elapsed ≥ cadence.
    /// Inactive alerts and unsupported intervals are never due.
    /// </summary>
    public static bool IsDue(PriceAlert alert, DateTime nowUtc)
    {
        if (!alert.IsActive)
            return false;

        if (!TryGetCadence(alert.Interval, out var cadence))
            return false;

        return alert.LastCheckedAt is null
            || nowUtc - alert.LastCheckedAt.Value >= cadence;
    }

    public const string UnsupportedIntervalMessage = "This interval is not supported yet.";
}
