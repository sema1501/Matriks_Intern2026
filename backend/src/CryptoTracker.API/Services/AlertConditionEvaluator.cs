using CryptoTracker.API.Models;

namespace CryptoTracker.API.Services;

public static class AlertConditionEvaluator
{
    /// <summary>
    /// Returns true when the alert's direction/target condition is satisfied
    /// for the given current price. Unknown directions return false.
    /// </summary>
    public static bool IsConditionSatisfied(PriceAlert alert, decimal currentPrice)
    {
        return alert.Direction switch
        {
            AlertDirection.Above => currentPrice >= alert.TargetPrice,
            AlertDirection.Below => currentPrice <= alert.TargetPrice,
            _ => false
        };
    }

    public static bool IsIntervalSupported(AlertInterval interval) =>
        interval == AlertInterval.Minute;

    public const string UnsupportedIntervalMessage = "This interval is not supported yet.";
}
