using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace CryptoTracker.API.Services;

public class BotService(
    AppDbContext db,
    IPortfolioService portfolioService,
    IOptions<TradingBotOptions> options) : IBotService
{
    private readonly int _expirationMinutes = options.Value.SignalExpirationMinutes;
    private bool IsRelational => db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";

    public async Task<List<BotResponse>> GetBotsByUserAsync(
        int userId, CancellationToken cancellationToken = default)
    {
        return await db.TradingBots
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => MapToResponse(b))
            .ToListAsync(cancellationToken);
    }

    public async Task<BotResponse> CreateBotAsync(
        int userId, CreateBotRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            throw new ArgumentException("Sembol boş olamaz.");

        if (request.TradeQuantity <= 0)
            throw new ArgumentException("İşlem miktarı sıfırdan büyük olmalıdır.");

        if (request.Strategy == BotStrategy.RsiThreshold)
        {
            if (request.BuyRsiThreshold < 0 || request.BuyRsiThreshold > 100)
                throw new ArgumentException("Alış RSI eşiği 0-100 arasında olmalıdır.");

            if (request.SellRsiThreshold < 0 || request.SellRsiThreshold > 100)
                throw new ArgumentException("Satış RSI eşiği 0-100 arasında olmalıdır.");

            if (request.BuyRsiThreshold >= request.SellRsiThreshold)
                throw new ArgumentException("Alış RSI eşiği, satış RSI eşiğinden düşük olmalıdır.");
        }
        else if (request.Strategy == BotStrategy.EmaCrossover)
        {
            if (request.ShortEmaPeriod is null || request.LongEmaPeriod is null)
                throw new ArgumentException("EMA stratejisi için kısa ve uzun periyot zorunludur.");

            if (request.ShortEmaPeriod < 1 || request.LongEmaPeriod < 1)
                throw new ArgumentException("EMA periyotları en az 1 olmalıdır.");

            if (request.ShortEmaPeriod >= request.LongEmaPeriod)
                throw new ArgumentException("Kısa EMA periyodu, uzun EMA periyodundan küçük olmalıdır.");
        }
        else
        {
            throw new ArgumentException($"Desteklenmeyen strateji: {request.Strategy}");
        }

        var normalizedSymbol = request.Symbol.Trim().ToUpperInvariant();

        var exists = await db.TradingBots.AnyAsync(
            b => b.UserId == userId && b.Symbol == normalizedSymbol,
            cancellationToken);

        if (exists)
            throw new InvalidOperationException("Bu sembol için zaten bir botunuz var.");

        var bot = new TradingBot
        {
            UserId = userId,
            Symbol = normalizedSymbol,
            Strategy = request.Strategy,
            BuyRsiThreshold = request.BuyRsiThreshold,
            SellRsiThreshold = request.SellRsiThreshold,
            ShortEmaPeriod = request.Strategy == BotStrategy.EmaCrossover
                ? request.ShortEmaPeriod
                : null,
            LongEmaPeriod = request.Strategy == BotStrategy.EmaCrossover
                ? request.LongEmaPeriod
                : null,
            TradeQuantity = request.TradeQuantity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        bot.Validate();

        db.TradingBots.Add(bot);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Bu sembol için zaten bir botunuz var.");
        }

        return MapToResponse(bot);
    }

    public async Task<BotResponse> ToggleBotAsync(
        int userId, int botId, CancellationToken cancellationToken = default)
    {
        var bot = await db.TradingBots
            .FirstOrDefaultAsync(b => b.Id == botId && b.UserId == userId, cancellationToken);

        if (bot is null)
            throw new KeyNotFoundException("Bot bulunamadı.");

        bot.IsActive = !bot.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        return MapToResponse(bot);
    }

    public async Task DeleteBotAsync(
        int userId, int botId, CancellationToken cancellationToken = default)
    {
        var bot = await db.TradingBots
            .FirstOrDefaultAsync(b => b.Id == botId && b.UserId == userId, cancellationToken);

        if (bot is null)
            throw new KeyNotFoundException("Bot bulunamadı.");

        db.TradingBots.Remove(bot);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<BotSignalResponse>> GetSignalsAsync(
        int userId, int botId, CancellationToken cancellationToken = default)
    {
        var botExists = await db.TradingBots
            .AnyAsync(b => b.Id == botId && b.UserId == userId, cancellationToken);

        if (!botExists)
            throw new KeyNotFoundException("Bot bulunamadı.");

        await ExpireStaleSignalsForBotAsync(botId, cancellationToken);

        return await db.BotSignals
            .AsNoTracking()
            .Where(s => s.BotId == botId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => MapToSignalResponse(s))
            .ToListAsync(cancellationToken);
    }

    public async Task<SignalActionResponse> ApproveSignalAsync(
        int userId, int signalId, CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? transaction = null;
        if (IsRelational)
            transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var signalData = await db.BotSignals
                .Include(s => s.Bot)
                .FirstOrDefaultAsync(s => s.Id == signalId && s.Bot.UserId == userId, cancellationToken);

            if (signalData is null)
                throw new KeyNotFoundException("Sinyal bulunamadı.");

            var cutoff = DateTime.UtcNow.AddMinutes(-_expirationMinutes);

            if (signalData.Status == BotSignalStatus.Pending && signalData.CreatedAt <= cutoff)
            {
                await AtomicStatusTransitionAsync(
                    signalId, userId, BotSignalStatus.Pending, BotSignalStatus.Expired, null, cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
                throw new InvalidOperationException("Sinyalin süresi dolmuş.");
            }

            if (signalData.Status != BotSignalStatus.Pending)
                throw new InvalidOperationException($"Sinyal zaten {signalData.Status} durumunda.");

            var claimed = await AtomicStatusTransitionAsync(
                signalId, userId, BotSignalStatus.Pending, BotSignalStatus.Approved, cutoff, cancellationToken);

            if (!claimed)
                throw new InvalidOperationException("Sinyal zaten işlenmiş.");

            TransactionDto tradeResult;

            try
            {
                tradeResult = signalData.SignalType == BotSignalType.Buy
                    ? await portfolioService.BuyAsync(
                        userId, signalData.Bot.Symbol, signalData.Bot.TradeQuantity,
                        signalData.PriceAtSignal, cancellationToken)
                    : await portfolioService.SellAsync(
                        userId, signalData.Bot.Symbol, signalData.Bot.TradeQuantity,
                        signalData.PriceAtSignal, cancellationToken);
            }
            catch
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    transaction = null;
                }
                else
                {
                    signalData.Status = BotSignalStatus.Pending;
                    await db.SaveChangesAsync(cancellationToken);
                }
                throw;
            }

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            return new SignalActionResponse(signalData.Id, BotSignalStatus.Approved, tradeResult);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    public async Task<SignalActionResponse> RejectSignalAsync(
        int userId, int signalId, CancellationToken cancellationToken = default)
    {
        var signal = await db.BotSignals
            .Include(s => s.Bot)
            .FirstOrDefaultAsync(s => s.Id == signalId && s.Bot.UserId == userId, cancellationToken);

        if (signal is null)
            throw new KeyNotFoundException("Sinyal bulunamadı.");

        var cutoff = DateTime.UtcNow.AddMinutes(-_expirationMinutes);

        if (signal.Status == BotSignalStatus.Pending && signal.CreatedAt <= cutoff)
        {
            await AtomicStatusTransitionAsync(
                signalId, userId, BotSignalStatus.Pending, BotSignalStatus.Expired, null, cancellationToken);
            throw new InvalidOperationException("Sinyalin süresi dolmuş.");
        }

        if (signal.Status != BotSignalStatus.Pending)
            throw new InvalidOperationException($"Sinyal zaten {signal.Status} durumunda.");

        var claimed = await AtomicStatusTransitionAsync(
            signalId, userId, BotSignalStatus.Pending, BotSignalStatus.Rejected, cutoff, cancellationToken);

        if (!claimed)
            throw new InvalidOperationException("Sinyal zaten işlenmiş.");

        return new SignalActionResponse(signal.Id, BotSignalStatus.Rejected, null);
    }

    private async Task<bool> AtomicStatusTransitionAsync(
        int signalId,
        int userId,
        BotSignalStatus from,
        BotSignalStatus to,
        DateTime? expirationCutoff,
        CancellationToken cancellationToken)
    {
        if (IsRelational)
        {
            var query = db.BotSignals
                .Where(s => s.Id == signalId
                    && s.Status == from
                    && db.TradingBots.Any(b => b.Id == s.BotId && b.UserId == userId));

            if (expirationCutoff is not null)
                query = query.Where(s => s.CreatedAt > expirationCutoff.Value);

            var affected = await query.ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.Status, to),
                cancellationToken);

            return affected == 1;
        }

        var signal = await db.BotSignals
            .FirstOrDefaultAsync(s => s.Id == signalId && s.Status == from, cancellationToken);

        if (signal is null)
            return false;

        if (expirationCutoff is not null && signal.CreatedAt <= expirationCutoff.Value)
            return false;

        signal.Status = to;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ExpireStaleSignalsForBotAsync(int botId, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-_expirationMinutes);
        var staleSignals = await db.BotSignals
            .Where(s => s.BotId == botId
                && s.Status == BotSignalStatus.Pending
                && s.CreatedAt <= cutoff)
            .ToListAsync(cancellationToken);

        if (staleSignals.Count == 0)
            return;

        foreach (var s in staleSignals)
            s.Status = BotSignalStatus.Expired;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BotPerformanceDto> GetBotPerformanceAsync(int userId, CancellationToken cancellationToken = default)
    {
        var userSignalsQuery = db.BotSignals
            .Include(s => s.Bot)
            .Where(s => s.Bot.UserId == userId);

        var total = await userSignalsQuery.CountAsync(cancellationToken);

        if (total == 0) return new BotPerformanceDto(0, 0, 0, 0, 0, 0, 0m, new List<BotActivePosition>());

        var approved = await userSignalsQuery.CountAsync(x => x.Status == BotSignalStatus.Approved, cancellationToken);
        var rejected = await userSignalsQuery.CountAsync(x => x.Status == BotSignalStatus.Rejected, cancellationToken);
        var expired = await userSignalsQuery.CountAsync(x => x.Status == BotSignalStatus.Expired, cancellationToken);
        var failed = await userSignalsQuery.CountAsync(x => x.Status == BotSignalStatus.Failed, cancellationToken);

        double approvalRate = Math.Round(((double)approved / total) * 100, 2);

        var approvedSignals = await userSignalsQuery
            .Where(x => x.Status == BotSignalStatus.Approved)
            .ToListAsync(cancellationToken);

        decimal botProfitLoss = 0m;
        var activePositions = new List<BotActivePosition>();

        var symbolGroups = approvedSignals.GroupBy(x => x.Bot.Symbol);

        foreach (var group in symbolGroups)
        {
            decimal buyTotalCost = group.Where(x => x.SignalType == BotSignalType.Buy).Sum(x => x.Bot.TradeQuantity * x.PriceAtSignal);
            decimal buyQty = group.Where(x => x.SignalType == BotSignalType.Buy).Sum(x => x.Bot.TradeQuantity);

            decimal sellTotalRevenue = group.Where(x => x.SignalType == BotSignalType.Sell).Sum(x => x.Bot.TradeQuantity * x.PriceAtSignal);
            decimal sellQty = group.Where(x => x.SignalType == BotSignalType.Sell).Sum(x => x.Bot.TradeQuantity);

            decimal currentQty = buyQty - sellQty;

            if (sellQty > 0 && buyQty > 0)
            {
                decimal avgBuyPrice = buyTotalCost / buyQty;
                decimal costOfSold = avgBuyPrice * sellQty;
                botProfitLoss += (sellTotalRevenue - costOfSold);
            }


            if (currentQty > 0)
            {
                decimal avgBuyPrice = buyQty > 0 ? (buyTotalCost / buyQty) : 0;
                activePositions.Add(new BotActivePosition(group.Key, currentQty, currentQty * avgBuyPrice));
            }
        }


        return new BotPerformanceDto(total, approved, rejected, expired, failed, approvalRate, botProfitLoss, activePositions);
    }

    private static BotResponse MapToResponse(TradingBot bot) =>
        new(bot.Id, bot.Symbol, bot.IsActive, bot.BuyRsiThreshold,
            bot.SellRsiThreshold, bot.TradeQuantity, bot.CreatedAt,
            bot.Strategy, bot.ShortEmaPeriod, bot.LongEmaPeriod);

    private static BotSignalResponse MapToSignalResponse(BotSignal signal) =>
        new(signal.Id, signal.BotId, signal.SignalType, signal.RsiValueAtSignal,
            signal.PriceAtSignal, signal.CreatedAt, signal.Status);
}

public class TradingBotOptions
{
    public const string SectionName = "TradingBot";
    public int SignalExpirationMinutes { get; set; } = 15;
}