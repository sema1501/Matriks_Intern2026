using CryptoTracker.API.Data;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

/// <summary>
/// Shared virtual-portfolio execution used by BotMonitorService and Development debug smoke.
/// Does not mutate TradingBot.IsActive.
/// </summary>
public interface IBotAutoTradeExecutor
{
    Task<BotSignal> ExecuteAsync(
        TradingBot bot,
        BotSignalType signalType,
        decimal price,
        decimal rsiValueAtSignal,
        CancellationToken cancellationToken = default);
}

public sealed class BotAutoTradeExecutor(
    AppDbContext db,
    IPortfolioService portfolioService) : IBotAutoTradeExecutor
{
    public async Task<BotSignal> ExecuteAsync(
        TradingBot bot,
        BotSignalType signalType,
        decimal price,
        decimal rsiValueAtSignal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);

        try
        {
            if (signalType == BotSignalType.Buy)
            {
                await portfolioService.BuyAsync(
                    bot.UserId,
                    bot.Symbol,
                    bot.TradeQuantity,
                    price,
                    cancellationToken);
            }
            else
            {
                await portfolioService.SellAsync(
                    bot.UserId,
                    bot.Symbol,
                    bot.TradeQuantity,
                    price,
                    cancellationToken);
            }

            var approved = new BotSignal
            {
                BotId = bot.Id,
                SignalType = signalType,
                RsiValueAtSignal = rsiValueAtSignal,
                PriceAtSignal = price,
                CreatedAt = DateTime.UtcNow,
                Status = BotSignalStatus.Approved
            };

            db.BotSignals.Add(approved);
            await db.SaveChangesAsync(cancellationToken);
            return approved;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            var failed = new BotSignal
            {
                BotId = bot.Id,
                SignalType = signalType,
                RsiValueAtSignal = rsiValueAtSignal,
                PriceAtSignal = price,
                CreatedAt = DateTime.UtcNow,
                Status = BotSignalStatus.Failed
            };

            db.BotSignals.Add(failed);
            await db.SaveChangesAsync(cancellationToken);
            return failed;
        }
    }
}
