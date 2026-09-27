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

    // ---------------- #82: AlertType.PercentChange (Görev 46) ----------------
    // Referans fiyat 100 → yüzde değişim = (fiyat - 100) / 100 * 100 → fiyat ile aynı sayı; hesap kolay okunur.

    [Theory]
    [InlineData(105, true)]    // tam eşik (+%5) → tetiklenir (>=)
    [InlineData(110, true)]    // +%10 → tetiklenir
    [InlineData(104.99, false)] // +%4.99 → eşiğe ulaşmadı
    [InlineData(90, false)]    // düşüş, Above için anlamsız
    public void PercentChange_Above_triggers_when_rise_reaches_threshold(double price, bool expected)
    {
        var alert = PercentAlert(AlertDirection.Above, reference: 100m, threshold: 5m);
        Assert.Equal(expected, AlertConditionEvaluator.IsConditionSatisfied(alert, (decimal)price));
    }

    [Theory]
    [InlineData(95, true)]     // tam eşik (-%5) → tetiklenir (<=)
    [InlineData(80, true)]     // -%20 → tetiklenir
    [InlineData(95.01, false)] // -%4.99 → eşiğe ulaşmadı
    [InlineData(110, false)]   // yükseliş, Below için anlamsız
    public void PercentChange_Below_triggers_when_drop_reaches_threshold(double price, bool expected)
    {
        var alert = PercentAlert(AlertDirection.Below, reference: 100m, threshold: 5m);
        Assert.Equal(expected, AlertConditionEvaluator.IsConditionSatisfied(alert, (decimal)price));
    }

    [Theory]
    [InlineData(AlertDirection.Above)]
    [InlineData(AlertDirection.Below)]
    public void PercentChange_missing_reference_price_returns_false(AlertDirection direction)
    {
        var alert = PercentAlert(direction, reference: null, threshold: 5m);
        // Fiyat ne olursa olsun tetiklenmemeli: referans yoksa yüzde hesaplanamaz.
        Assert.False(AlertConditionEvaluator.IsConditionSatisfied(alert, 1_000_000m));
        Assert.False(AlertConditionEvaluator.IsConditionSatisfied(alert, 0.0001m));
    }

    [Theory]
    [InlineData(0)]    // sıfıra bölme olurdu
    [InlineData(-50)]  // negatif fiyat anlamsız
    public void PercentChange_zero_or_negative_reference_price_returns_false(double reference)
    {
        var alert = PercentAlert(AlertDirection.Above, (decimal)reference, threshold: 5m);
        var ex = Record.Exception(() => AlertConditionEvaluator.IsConditionSatisfied(alert, 200m));

        Assert.Null(ex); // DivideByZeroException fırlatmamalı
        Assert.False(AlertConditionEvaluator.IsConditionSatisfied(alert, 200m));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]  // %0 eşik her fiyatta tetiklenirdi → reddedilir
    [InlineData(-5.0)]
    public void PercentChange_missing_zero_or_negative_threshold_returns_false(double? threshold)
    {
        var alert = PercentAlert(AlertDirection.Above, reference: 100m, threshold: (decimal?)threshold);
        Assert.False(AlertConditionEvaluator.IsConditionSatisfied(alert, 200m));
    }

    [Fact]
    public void PercentChange_ignores_TargetPrice_field()
    {
        // TargetPrice yüzde alarmında kullanılmaz; yanlışlıkla fiyat mantığına düşmediğini doğrular.
        var alert = PercentAlert(AlertDirection.Above, reference: 100m, threshold: 50m);
        alert.TargetPrice = 101m;

        Assert.False(AlertConditionEvaluator.IsConditionSatisfied(alert, 120m)); // +%20 < %50
    }

    private static PriceAlert PercentAlert(AlertDirection direction, decimal? reference, decimal? threshold) => new()
    {
        Symbol = "BTCUSDT",
        Type = AlertType.PercentChange,
        Direction = direction,
        ReferencePrice = reference,
        PercentChangeThreshold = threshold,
        IsActive = true,
        Interval = AlertInterval.Minute
    };

    private static PriceAlert Alert(AlertDirection direction, decimal target) => new()
    {
        Symbol = "BTCUSDT",
        TargetPrice = target,
        Direction = direction,
        IsActive = true,
        Interval = AlertInterval.Minute
    };
}
