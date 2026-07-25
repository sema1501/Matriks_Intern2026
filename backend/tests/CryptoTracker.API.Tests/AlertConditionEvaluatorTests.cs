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
    [InlineData(AlertInterval.Hourly, true)]
    [InlineData(AlertInterval.Daily, true)]
    [InlineData((AlertInterval)99, false)]
    public void Interval_support_includes_minute_hourly_daily(AlertInterval interval, bool expected)
    {
        Assert.Equal(expected, AlertConditionEvaluator.IsIntervalSupported(interval));
    }

    [Fact]
    public void IsDue_null_LastCheckedAt_is_immediately_due()
    {
        var alert = Alert(AlertDirection.Above, 100m);
        alert.Interval = AlertInterval.Hourly;
        alert.LastCheckedAt = null;

        Assert.True(AlertConditionEvaluator.IsDue(alert, new DateTime(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void IsDue_respects_cadence_thresholds()
    {
        var t0 = new DateTime(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc);
        var hourly = Alert(AlertDirection.Above, 100m);
        hourly.Interval = AlertInterval.Hourly;
        hourly.LastCheckedAt = t0;

        Assert.False(AlertConditionEvaluator.IsDue(hourly, t0.AddMinutes(59)));
        Assert.True(AlertConditionEvaluator.IsDue(hourly, t0.AddHours(1)));

        var daily = Alert(AlertDirection.Above, 100m);
        daily.Interval = AlertInterval.Daily;
        daily.LastCheckedAt = t0;

        Assert.False(AlertConditionEvaluator.IsDue(daily, t0.AddHours(23)));
        Assert.True(AlertConditionEvaluator.IsDue(daily, t0.AddDays(1)));
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
