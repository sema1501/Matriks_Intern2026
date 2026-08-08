using CryptoTracker.API.Data;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public sealed class BotMonitorService(
    IServiceScopeFactory scopeFactory,
    IBinanceKlineService klineService,
    ILogger<BotMonitorService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);
    private const int RsiPeriod = 14;
    private const int KlineLimit = 100;
    private const string KlineInterval = "1m";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Bot monitor service started.");

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while evaluating trading bots.");
        }
    }

    private async Task EvaluateBotsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var portfolioService = scope.ServiceProvider.GetRequiredService<IPortfolioService>();

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
                var closingPrices = await klineService.GetClosingPricesAsync(
                    symbol,
                    KlineInterval,
                    KlineLimit,
                    cancellationToken);

                if (closingPrices.Count < RsiPeriod + 1)
                {
                    logger.LogWarning("Not enough closing prices to calculate RSI for {Symbol}. Count: {Count}", symbol, closingPrices.Count);
                    continue;
                }

                var rsi = RsiCalculator.Calculate(closingPrices, RsiPeriod);
                var currentPrice = closingPrices[^1];

                foreach (var bot in symbolGroup)
                {
                    var signalType = DetermineSignalType(bot, rsi);

                    if (signalType is null)
                        continue;

                    logger.LogInformation("Bot {BotId} triggered {SignalType} signal for {Symbol} at price {Price}. Executing directly on Binance Testnet...", 
                        bot.Id, signalType.Value, symbol, currentPrice);

                    try
                    {
                        if (signalType == BotSignalType.Buy)
                        {
                            await portfolioService.BuyAsync(
                                userId: bot.UserId,
                                symbol: symbol,
                                quantity: bot.TradeQuantity,
                                pricePerUnit: currentPrice,
                                cancellationToken: cancellationToken
                            );
                        }
                        else if (signalType == BotSignalType.Sell)
                        {
                            await portfolioService.SellAsync(
                                userId: bot.UserId,
                                symbol: symbol,
                                quantity: bot.TradeQuantity,
                                pricePerUnit: currentPrice,
                                cancellationToken: cancellationToken
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to execute automated order for Bot {BotId} on symbol {Symbol}.", bot.Id, symbol);
                    }
                }

                logger.LogInformation("Evaluated {BotCount} active bots for {Symbol}. RSI: {Rsi}, Price: {Price}", symbolGroup.Count(), symbol, rsi, currentPrice);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to evaluate trading bots for symbol {Symbol}.", symbol);
            }
        }
    }

    private static BotSignalType? DetermineSignalType(TradingBot bot, decimal rsi)
    {
        if (rsi <= bot.BuyRsiThreshold)
            return BotSignalType.Buy;

        if (rsi >= bot.SellRsiThreshold)
            return BotSignalType.Sell;

        return null;
    }
}