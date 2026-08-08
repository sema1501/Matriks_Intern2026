using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public interface IBotDebugExecuteService
{
    Task<DebugBotExecuteResponse> ExecuteAsync(
        int userId,
        int botId,
        DebugBotExecuteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// DEVELOPMENT / DEBUG ONLY: same zone-entry decision as BotMonitorService,
    /// then the same BotAutoTradeExecutor path when a signal is detected.
    /// </summary>
    Task<DebugZoneEntryResponse> EvaluateZoneEntryAsync(
        int userId,
        int botId,
        DebugZoneEntryRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Development smoke helpers for automatic virtual portfolio execution.
/// Does not change TradingBot.IsActive.
/// </summary>
public sealed class BotDebugExecuteService(
    AppDbContext db,
    IBinancePriceService priceService,
    IBotAutoTradeExecutor tradeExecutor) : IBotDebugExecuteService
{
    public async Task<DebugBotExecuteResponse> ExecuteAsync(
        int userId,
        int botId,
        DebugBotExecuteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var signalType = ParseSignalType(request.SignalType);
        var bot = await LoadOwnedBotAsync(userId, botId, cancellationToken);
        var wasActive = bot.IsActive;
        var price = await GetPublicPriceAsync(bot.Symbol, cancellationToken);

        // Debug smoke has no RSI context; 0 marks non-strategy forced execution.
        var signal = await tradeExecutor.ExecuteAsync(
            bot,
            signalType,
            price,
            rsiValueAtSignal: 0m,
            cancellationToken);

        var reloaded = await AssertIsActiveUnchangedAsync(bot.Id, wasActive, cancellationToken);

        return new DebugBotExecuteResponse(
            "DEVELOPMENT / DEBUG ONLY: forced virtual portfolio execution completed.",
            bot.Id,
            reloaded.IsActive,
            ToSignalResponse(signal));
    }

    public async Task<DebugZoneEntryResponse> EvaluateZoneEntryAsync(
        int userId,
        int botId,
        DebugZoneEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bot = await LoadOwnedBotAsync(userId, botId, cancellationToken);
        var wasActive = bot.IsActive;

        // Same production decision used by BotMonitorService.ProcessBotSignalAsync.
        var signalType = RsiSignalEvaluator.DetermineZoneEntrySignal(
            request.CurrentRsi,
            request.PreviousRsi,
            bot.BuyRsiThreshold,
            bot.SellRsiThreshold);

        if (signalType is null)
        {
            return new DebugZoneEntryResponse(
                "DEVELOPMENT / DEBUG ONLY: no zone-entry signal for the provided RSI pair.",
                SignalDetected: false,
                SignalType: null,
                bot.Id,
                wasActive,
                bot.BuyRsiThreshold,
                bot.SellRsiThreshold,
                request.PreviousRsi,
                request.CurrentRsi,
                Signal: null);
        }

        var price = await GetPublicPriceAsync(bot.Symbol, cancellationToken);

        // Same automatic execution path as BotMonitorService.
        var signal = await tradeExecutor.ExecuteAsync(
            bot,
            signalType.Value,
            price,
            rsiValueAtSignal: request.CurrentRsi,
            cancellationToken);

        var reloaded = await AssertIsActiveUnchangedAsync(bot.Id, wasActive, cancellationToken);

        return new DebugZoneEntryResponse(
            "DEVELOPMENT / DEBUG ONLY: zone-entry detected; automatic virtual execution completed.",
            SignalDetected: true,
            signalType.Value,
            bot.Id,
            reloaded.IsActive,
            bot.BuyRsiThreshold,
            bot.SellRsiThreshold,
            request.PreviousRsi,
            request.CurrentRsi,
            ToSignalResponse(signal));
    }

    private async Task<TradingBot> LoadOwnedBotAsync(
        int userId,
        int botId,
        CancellationToken cancellationToken)
    {
        var bot = await db.TradingBots
            .FirstOrDefaultAsync(b => b.Id == botId && b.UserId == userId, cancellationToken);

        if (bot is null)
            throw new KeyNotFoundException("Bot bulunamadı.");

        return bot;
    }

    private async Task<decimal> GetPublicPriceAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var prices = await priceService.GetPricesAsync(
            new[] { symbol },
            cancellationToken);

        if (!prices.TryGetValue(symbol.Trim().ToUpperInvariant(), out var price) || price <= 0)
        {
            throw new InvalidOperationException(
                "Binance üzerinden sembol fiyatı alınamadı.");
        }

        return price;
    }

    private async Task<TradingBot> AssertIsActiveUnchangedAsync(
        int botId,
        bool wasActive,
        CancellationToken cancellationToken)
    {
        var reloaded = await db.TradingBots
            .AsNoTracking()
            .SingleAsync(b => b.Id == botId, cancellationToken);

        if (reloaded.IsActive != wasActive)
        {
            throw new InvalidOperationException(
                "Unexpected bot IsActive mutation during debug execute.");
        }

        return reloaded;
    }

    private static BotSignalResponse ToSignalResponse(BotSignal signal) =>
        new(
            signal.Id,
            signal.BotId,
            signal.SignalType,
            signal.RsiValueAtSignal,
            signal.PriceAtSignal,
            signal.CreatedAt,
            signal.Status);

    private static BotSignalType ParseSignalType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("signalType is required (BUY or SELL).");

        return raw.Trim().ToUpperInvariant() switch
        {
            "BUY" or "0" => BotSignalType.Buy,
            "SELL" or "1" => BotSignalType.Sell,
            _ => throw new ArgumentException("signalType must be BUY or SELL.")
        };
    }
}
