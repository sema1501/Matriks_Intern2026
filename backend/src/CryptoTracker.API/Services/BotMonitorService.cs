using CryptoTracker.API.Data;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CryptoTracker.API.Services;

/// <summary>
/// Evaluates active bots and automatically executes virtual portfolio trades.
/// Supports two strategies: RSI threshold (zone entry) and EMA crossover.
/// No manual approve/reject step is required for newly generated signals.
/// </summary>
public sealed class BotMonitorService(
    IServiceScopeFactory scopeFactory,
    IBinanceKlineService klineService,
    IOptions<TradingBotOptions> botOptions,
    ILogger<BotMonitorService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);
    private readonly int _expirationMinutes = botOptions.Value.SignalExpirationMinutes;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Bot monitor service started (automatic portfolio execution).");

        await EvaluateBotsSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await EvaluateBotsSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Bot monitor service stopped.");
        }
    }

    private async Task EvaluateBotsSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EvaluateBotsAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "An unexpected error occurred while evaluating trading bots.");
        }
    }

    private async Task EvaluateBotsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tradeExecutor =
            scope.ServiceProvider.GetRequiredService<IBotAutoTradeExecutor>();

        // Legacy Pending signals (pre-auto-execution) may still expire.
        await ExpireLegacyPendingSignalsAsync(dbContext, cancellationToken);

        var activeBots = await dbContext.TradingBots
            .AsNoTracking()
            .Where(bot => bot.IsActive)
            .ToListAsync(cancellationToken);

        if (activeBots.Count == 0)
        {
            logger.LogDebug("No active trading bots were found.");
            return;
        }

        var botsBySymbol = activeBots
            .Where(bot => !string.IsNullOrWhiteSpace(bot.Symbol))
            .GroupBy(
                bot => bot.Symbol.Trim().ToUpperInvariant(),
                StringComparer.Ordinal);

        foreach (var symbolGroup in botsBySymbol)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = symbolGroup.Key;

            try
            {
                // EMA bots may need more candles than the RSI default.
                var klineLimit = DetermineKlineLimit(symbolGroup);

                var closingPrices =
                    await klineService.GetClosingPricesAsync(
                        symbol,
                        RsiSignalEvaluator.Interval,
                        klineLimit,
                        cancellationToken);

                if (closingPrices.Count < RsiSignalEvaluator.Period + 1)
                {
                    logger.LogWarning(
                        "Not enough closing prices to calculate RSI for {Symbol}. Count: {Count}",
                        symbol,
                        closingPrices.Count);

                    continue;
                }

                var rsiSeries = RsiCalculator.CalculateSeries(
                    closingPrices,
                    RsiSignalEvaluator.Period);

                var currentRsi = rsiSeries[^1];
                if (currentRsi is null)
                {
                    logger.LogWarning(
                        "RSI series did not produce a current value for {Symbol}.",
                        symbol);
                    continue;
                }

                // Previous closed candle RSI — survives process restarts (derived from market data).
                decimal? previousRsi = rsiSeries.Count >= 2
                    ? rsiSeries[^2]
                    : null;

                var currentPrice = closingPrices[^1];

                foreach (var bot in symbolGroup)
                {
                    switch (bot.Strategy)
                    {
                        case BotStrategy.RsiThreshold:
                            await ProcessBotSignalAsync(
                                dbContext,
                                tradeExecutor,
                                bot,
                                symbol,
                                currentRsi.Value,
                                previousRsi,
                                currentPrice,
                                cancellationToken);
                            break;

                        case BotStrategy.EmaCrossover:
                            await ProcessEmaBotSignalAsync(
                                dbContext,
                                tradeExecutor,
                                bot,
                                symbol,
                                closingPrices,
                                currentRsi.Value,
                                currentPrice,
                                cancellationToken);
                            break;

                        default:
                            // Logged rather than thrown: one misconfigured bot
                            // must not stop the others in this symbol group.
                            logger.LogError(
                                "Bot {BotId} has an unsupported strategy: {Strategy}.",
                                bot.Id,
                                bot.Strategy);
                            break;
                    }
                }

                logger.LogInformation(
                    "Evaluated {BotCount} active bots for {Symbol}. RSI: {Rsi}, Price: {Price}",
                    symbolGroup.Count(),
                    symbol,
                    currentRsi.Value,
                    currentPrice);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to evaluate trading bots for symbol {Symbol}.",
                    symbol);
            }
        }
    }

    private async Task ProcessBotSignalAsync(
        AppDbContext dbContext,
        IBotAutoTradeExecutor tradeExecutor,
        TradingBot bot,
        string symbol,
        decimal currentRsi,
        decimal? previousRsi,
        decimal currentPrice,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentSignalType = RsiSignalEvaluator.DetermineSignalType(
                currentRsi,
                bot.BuyRsiThreshold,
                bot.SellRsiThreshold);

            var signalType = RsiSignalEvaluator.DetermineZoneEntrySignal(
                currentRsi,
                previousRsi,
                bot.BuyRsiThreshold,
                bot.SellRsiThreshold);

            if (signalType is null)
            {
                var reason = DescribeZoneEntryNullReason(
                    currentSignalType,
                    previousRsi,
                    bot.BuyRsiThreshold,
                    bot.SellRsiThreshold);

                logger.LogInformation(
                    "Zone-entry diagnostic for bot {BotId}: PreviousRsi={PreviousRsi}, CurrentRsi={CurrentRsi}, BuyThreshold={BuyThreshold}, SellThreshold={SellThreshold}, CurrentSignalType={CurrentSignalType}, ZoneEntry=null, Reason={Reason}",
                    bot.Id,
                    previousRsi,
                    currentRsi,
                    bot.BuyRsiThreshold,
                    bot.SellRsiThreshold,
                    FormatSignalType(currentSignalType),
                    reason);
                return;
            }

            // Zone-entry is primary debounce. Extra guards:
            // 1) legacy Pending of same type
            // 2) same last bar already executed (Approved/Failed) — prevents 60s re-fire
            //    on an unchanged candle pair after restart or repeated polls.
            var pendingExists = await dbContext.BotSignals.AnyAsync(
                signal =>
                    signal.BotId == bot.Id &&
                    signal.SignalType == signalType.Value &&
                    signal.Status == BotSignalStatus.Pending,
                cancellationToken);

            if (pendingExists)
            {
                logger.LogDebug(
                    "Skipping auto-execution for bot {BotId}: legacy pending {SignalType} exists.",
                    bot.Id,
                    signalType.Value);
                return;
            }

            var alreadyExecutedThisBar = await dbContext.BotSignals.AnyAsync(
                signal =>
                    signal.BotId == bot.Id &&
                    signal.SignalType == signalType.Value &&
                    (signal.Status == BotSignalStatus.Approved ||
                     signal.Status == BotSignalStatus.Failed) &&
                    signal.PriceAtSignal == currentPrice &&
                    signal.RsiValueAtSignal == currentRsi,
                cancellationToken);

            if (alreadyExecutedThisBar)
            {
                logger.LogDebug(
                    "Skipping auto-execution for bot {BotId}: {SignalType} already processed for this bar.",
                    bot.Id,
                    signalType.Value);
                return;
            }

            logger.LogInformation(
                "Zone-entry {SignalType} for bot {BotId} user {UserId} {Symbol} RSI={Rsi} Price={Price}",
                signalType.Value,
                bot.Id,
                bot.UserId,
                symbol,
                currentRsi,
                currentPrice);

            var signal = await tradeExecutor.ExecuteAsync(
                bot,
                signalType.Value,
                currentPrice,
                currentRsi,
                cancellationToken);

            if (signal.Status == BotSignalStatus.Approved)
            {
                logger.LogInformation(
                    "Auto-executed {SignalType} for bot {BotId} user {UserId} {Symbol}.",
                    signalType.Value,
                    bot.Id,
                    bot.UserId,
                    symbol);
            }
            else
            {
                logger.LogWarning(
                    "Auto-trade failed for bot {BotId} user {UserId} {Symbol} {SignalType}. Status={Status}",
                    bot.Id,
                    bot.UserId,
                    symbol,
                    signalType.Value,
                    signal.Status);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One bot must not stop monitoring of others.
            logger.LogError(
                ex,
                "Unexpected error while auto-executing signal for bot {BotId} user {UserId} {Symbol}.",
                bot.Id,
                bot.UserId,
                symbol);
        }
    }

    /// <summary>
    /// EMA bots with a long period larger than the RSI default need more candles.
    /// Returns the largest requirement across every bot in the symbol group.
    /// </summary>
    private static int DetermineKlineLimit(IEnumerable<TradingBot> bots)
    {
        var limit = RsiSignalEvaluator.LiveKlineLimit;

        foreach (var bot in bots)
        {
            if (bot.Strategy != BotStrategy.EmaCrossover || bot.LongEmaPeriod is null)
                continue;

            var required =
                EmaCrossoverEvaluator.RequiredCandleCount(bot.LongEmaPeriod.Value) +
                EmaCrossoverEvaluator.SeedWarmupCandles;

            if (required > limit)
                limit = required;
        }

        return limit;
    }

    /// <summary>
    /// EMA crossover evaluation. Mirrors ProcessBotSignalAsync, including its
    /// duplicate-execution guards, so both strategies behave consistently.
    /// </summary>
    private async Task ProcessEmaBotSignalAsync(
        AppDbContext dbContext,
        IBotAutoTradeExecutor tradeExecutor,
        TradingBot bot,
        string symbol,
        IReadOnlyList<decimal> closingPrices,
        decimal currentRsi,
        decimal currentPrice,
        CancellationToken cancellationToken)
    {
        try
        {
            if (bot.ShortEmaPeriod is null || bot.LongEmaPeriod is null)
            {
                logger.LogWarning(
                    "Bot {BotId} uses EmaCrossover but its EMA periods are not configured.",
                    bot.Id);
                return;
            }

            var shortPeriod = bot.ShortEmaPeriod.Value;
            var longPeriod = bot.LongEmaPeriod.Value;

            if (shortPeriod >= longPeriod)
            {
                logger.LogWarning(
                    "Bot {BotId} has an invalid EMA configuration: short {Short} >= long {Long}.",
                    bot.Id,
                    shortPeriod,
                    longPeriod);
                return;
            }

            var requiredCandles = EmaCrossoverEvaluator.RequiredCandleCount(longPeriod);

            if (closingPrices.Count < requiredCandles)
            {
                logger.LogWarning(
                    "Not enough closing prices for EMA bot {BotId} on {Symbol}. Have {Have}, need {Need}.",
                    bot.Id,
                    symbol,
                    closingPrices.Count,
                    requiredCandles);
                return;
            }

            var shortEma = EmaCalculator.CalculateSeries(closingPrices, shortPeriod);
            var longEma = EmaCalculator.CalculateSeries(closingPrices, longPeriod);

            var last = closingPrices.Count - 1;

            var signalType = EmaCrossoverEvaluator.DetermineCrossoverSignal(
                shortEma[last - 1],
                longEma[last - 1],
                shortEma[last],
                longEma[last]);

            if (signalType is null)
            {
                logger.LogInformation(
                    "No EMA crossover for bot {BotId} on {Symbol}. " +
                    "PrevShort={PrevShort}, PrevLong={PrevLong}, Short={Short}, Long={Long}",
                    bot.Id,
                    symbol,
                    shortEma[last - 1],
                    longEma[last - 1],
                    shortEma[last],
                    longEma[last]);
                return;
            }

            // Same guards as the RSI path.
            var pendingExists = await dbContext.BotSignals.AnyAsync(
                signal =>
                    signal.BotId == bot.Id &&
                    signal.SignalType == signalType.Value &&
                    signal.Status == BotSignalStatus.Pending,
                cancellationToken);

            if (pendingExists)
            {
                logger.LogDebug(
                    "Skipping EMA auto-execution for bot {BotId}: legacy pending {SignalType} exists.",
                    bot.Id,
                    signalType.Value);
                return;
            }

            var alreadyExecutedThisBar = await dbContext.BotSignals.AnyAsync(
                signal =>
                    signal.BotId == bot.Id &&
                    signal.SignalType == signalType.Value &&
                    (signal.Status == BotSignalStatus.Approved ||
                     signal.Status == BotSignalStatus.Failed) &&
                    signal.PriceAtSignal == currentPrice &&
                    signal.RsiValueAtSignal == currentRsi,
                cancellationToken);

            if (alreadyExecutedThisBar)
            {
                logger.LogDebug(
                    "Skipping EMA auto-execution for bot {BotId}: {SignalType} already processed for this bar.",
                    bot.Id,
                    signalType.Value);
                return;
            }

            logger.LogInformation(
                "EMA crossover {SignalType} for bot {BotId} user {UserId} {Symbol}. " +
                "EMA{Short}={ShortValue} EMA{Long}={LongValue} Price={Price}",
                signalType.Value,
                bot.Id,
                bot.UserId,
                symbol,
                shortPeriod,
                shortEma[last],
                longPeriod,
                longEma[last],
                currentPrice);

            // RsiValueAtSignal stores the RSI observed at signal time. It did not
            // trigger this signal, but the recorded value is accurate and the
            // shared BotSignal table stays unchanged.
            var signal = await tradeExecutor.ExecuteAsync(
                bot,
                signalType.Value,
                currentPrice,
                currentRsi,
                cancellationToken);

            if (signal.Status == BotSignalStatus.Approved)
            {
                logger.LogInformation(
                    "Auto-executed EMA {SignalType} for bot {BotId} user {UserId} {Symbol}.",
                    signalType.Value,
                    bot.Id,
                    bot.UserId,
                    symbol);
            }
            else
            {
                logger.LogWarning(
                    "EMA auto-trade failed for bot {BotId} user {UserId} {Symbol} {SignalType}. Status={Status}",
                    bot.Id,
                    bot.UserId,
                    symbol,
                    signalType.Value,
                    signal.Status);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One bot must not stop monitoring of others.
            logger.LogError(
                ex,
                "Unexpected error while auto-executing EMA signal for bot {BotId} user {UserId} {Symbol}.",
                bot.Id,
                bot.UserId,
                symbol);
        }
    }

    private static string FormatSignalType(BotSignalType? signalType) =>
        signalType switch
        {
            BotSignalType.Buy => "BUY",
            BotSignalType.Sell => "SELL",
            null => "None",
            _ => signalType.ToString() ?? "None"
        };

    /// <summary>
    /// Diagnostic-only explanation of why DetermineZoneEntrySignal returned null.
    /// Mirrors evaluator rules; does not affect trading decisions.
    /// </summary>
    private static string DescribeZoneEntryNullReason(
        BotSignalType? currentSignalType,
        decimal? previousRsi,
        decimal buyThreshold,
        decimal sellThreshold)
    {
        if (currentSignalType is null)
            return "Current RSI is between thresholds.";

        if (previousRsi is null)
        {
            // DetermineZoneEntrySignal emits when previous is null and current is in a zone;
            // this branch should not occur when ZoneEntry is null.
            return "Unexpected: previous RSI was null while current RSI is in a zone.";
        }

        if (currentSignalType == BotSignalType.Buy)
        {
            return previousRsi.Value <= buyThreshold
                ? "BUY rejected because previous RSI was already inside buy zone."
                : "BUY rejected (previous RSI did not qualify as zone entry).";
        }

        return previousRsi.Value >= sellThreshold
            ? "SELL rejected because previous RSI was already inside sell zone."
            : "SELL rejected (previous RSI did not qualify as zone entry).";
    }

    private async Task ExpireLegacyPendingSignalsAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-_expirationMinutes);
        var staleSignals = await dbContext.BotSignals
            .Where(s => s.Status == BotSignalStatus.Pending && s.CreatedAt <= cutoff)
            .ToListAsync(cancellationToken);

        if (staleSignals.Count == 0)
            return;

        foreach (var s in staleSignals)
            s.Status = BotSignalStatus.Expired;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Expired {Count} legacy pending signal(s).",
            staleSignals.Count);
    }
}