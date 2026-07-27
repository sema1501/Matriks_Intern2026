using CryptoTracker.API.Data;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CryptoTracker.API.Services;

public class AlertMonitoringOptions
{
    public const string SectionName = "AlertMonitoring";

    /// <summary>
    /// Monitoring cycle length in seconds. Default is 60 for production.
    /// Override locally via configuration for faster development tests.
    /// </summary>
    public int IntervalSeconds { get; set; } = 60;
}

/// <summary>
/// Background worker that evaluates due active alerts every cycle.
/// First cycle runs after the configured delay (default 60s), then every IntervalSeconds.
/// Cycles never overlap; each cycle creates a fresh DI scope for DbContext.
/// </summary>
public class AlertMonitorService(
    IServiceScopeFactory scopeFactory,
    IOptions<AlertMonitoringOptions> options,
    ILogger<AlertMonitorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(1, options.Value.IntervalSeconds);
        logger.LogInformation(
            "AlertMonitorService started. IntervalSeconds={IntervalSeconds}. First cycle after initial delay.",
            intervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Alert monitoring cycle failed; will retry on next interval");
            }
        }

        logger.LogInformation("AlertMonitorService stopping");
    }

    internal async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var binance = scope.ServiceProvider.GetRequiredService<IBinancePriceService>();
        var processor = scope.ServiceProvider.GetRequiredService<IAlertMonitoringProcessor>();

        await processor.ProcessAsync(db, binance, cancellationToken);
    }
}

public interface IAlertMonitoringProcessor
{
    Task<AlertMonitoringCycleResult> ProcessAsync(
        AppDbContext db,
        IBinancePriceService binance,
        CancellationToken cancellationToken);
}

public record AlertMonitoringCycleResult(
    int ActiveAlertCount,
    int UniqueSymbolCount,
    int GeneratedSignalCount,
    int SkippedAlertCount);

public class AlertMonitoringProcessor(
    ILogger<AlertMonitoringProcessor> logger,
    IClock clock) : IAlertMonitoringProcessor
{
    public async Task<AlertMonitoringCycleResult> ProcessAsync(
        AppDbContext db,
        IBinancePriceService binance,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        // Tracked entities so LastCheckedAt updates persist in the same SaveChanges batch.
        var activeAlerts = await db.PriceAlerts
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);

        if (activeAlerts.Count == 0)
        {
            logger.LogDebug("Alert monitoring cycle: no active alerts");
            return new AlertMonitoringCycleResult(0, 0, 0, 0);
        }

        var dueAlerts = new List<PriceAlert>();
        var skipped = 0;

        foreach (var alert in activeAlerts)
        {
            if (!AlertConditionEvaluator.TryGetCadence(alert.Interval, out _))
            {
                skipped++;
                logger.LogWarning(
                    "Skipping alert {AlertId} with unsupported interval value {Interval}",
                    alert.Id,
                    (int)alert.Interval);
                continue;
            }

            if (AlertConditionEvaluator.IsDue(alert, now))
                dueAlerts.Add(alert);
        }

        if (dueAlerts.Count == 0)
        {
            logger.LogDebug(
                "Alert monitoring cycle: {ActiveCount} active alerts, none due this tick",
                activeAlerts.Count);
            return new AlertMonitoringCycleResult(0, 0, 0, skipped);
        }

        var symbols = dueAlerts
            .Select(a => a.Symbol.Trim().ToUpperInvariant())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        IReadOnlyDictionary<string, decimal> prices;
        try
        {
            prices = await binance.GetPricesAsync(symbols, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Binance lookup failed for {SymbolCount} symbols; skipping cycle writes", symbols.Count);
            return new AlertMonitoringCycleResult(dueAlerts.Count, symbols.Count, 0, dueAlerts.Count + skipped);
        }

        var signals = new List<AlertSignal>();
        var checkedAny = false;

        foreach (var alert in dueAlerts)
        {
            try
            {
                var symbol = alert.Symbol.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(symbol) || !prices.TryGetValue(symbol, out var currentPrice))
                {
                    skipped++;
                    continue;
                }

                // Advance cadence only after a real price evaluation (condition met or not).
                alert.LastCheckedAt = now;
                checkedAny = true;

                if (!AlertConditionEvaluator.IsConditionSatisfied(alert, currentPrice))
                    continue;

                signals.Add(new AlertSignal
                {
                    AlertId = alert.Id,
                    PriceAtTrigger = currentPrice,
                    TriggeredAt = now
                });
            }
            catch (Exception ex)
            {
                skipped++;
                logger.LogWarning(ex, "Skipping malformed alert {AlertId}", alert.Id);
            }
        }

        if (signals.Count > 0)
            db.AlertSignals.AddRange(signals);

        if (signals.Count > 0 || checkedAny)
            await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Alert monitoring cycle complete: DueAlerts={ActiveAlertCount}, UniqueSymbols={UniqueSymbolCount}, GeneratedSignals={GeneratedSignalCount}, Skipped={SkippedAlertCount}",
            dueAlerts.Count,
            symbols.Count,
            signals.Count,
            skipped);

        return new AlertMonitoringCycleResult(dueAlerts.Count, symbols.Count, signals.Count, skipped);
    }
}
