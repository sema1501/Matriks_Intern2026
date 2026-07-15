using CryptoTracker.API.Models;
using CryptoTracker.API.Services;

namespace CryptoTracker.API.Tests;

public class AlertConditionEvaluatorTests
{
    [Fact]
    public void Above_triggers_when_price_is_greater_or_equal()
    {
        var alert = Alert(AlertDirection.Above, 100m);
        Assert.True(AlertConditionEvaluator.IsConditionSatisfied(alert, 100m));
        Assert.True(AlertConditionEvaluator.IsConditionSatisfied(alert, 150m));
    }

    [Fact]
    public void Above_does_not_trigger_below_target()
    {
        var alert = Alert(AlertDirection.Above, 100m);
        Assert.False(AlertConditionEvaluator.IsConditionSatisfied(alert, 99.99m));
    }

    [Fact]
    public void Below_triggers_when_price_is_less_or_equal()
    {
        var alert = Alert(AlertDirection.Below, 100m);
        Assert.True(AlertConditionEvaluator.IsConditionSatisfied(alert, 100m));
        Assert.True(AlertConditionEvaluator.IsConditionSatisfied(alert, 50m));
    }

    [Fact]
    public void Below_does_not_trigger_above_target()
    {
        var alert = Alert(AlertDirection.Below, 100m);
        Assert.False(AlertConditionEvaluator.IsConditionSatisfied(alert, 100.01m));
    }

    [Theory]
    [InlineData(AlertInterval.Minute, true)]
    [InlineData(AlertInterval.Hourly, false)]
    [InlineData(AlertInterval.Daily, false)]
    public void Interval_support_matches_week4_scope(AlertInterval interval, bool expected)
    {
        Assert.Equal(expected, AlertConditionEvaluator.IsIntervalSupported(interval));
    }

    private static PriceAlert Alert(AlertDirection direction, decimal target) => new()
    {
        Symbol = "BTCUSDT",
        TargetPrice = target,
        Direction = direction,
        IsActive = true,
        Interval = AlertInterval.Minute
    };
}
