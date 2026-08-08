using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace CryptoTracker.API.Services;

public class PortfolioService(AppDbContext db, IBinanceTestnetClient binanceClient) : IPortfolioService
{
    public async Task<decimal> GetBalanceAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        return user.VirtualBalance;
    }

    public async Task<List<HoldingDto>> GetHoldingsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await db.PortfolioHoldings
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderBy(h => h.Symbol)
            .Select(h => new HoldingDto(h.Symbol, h.Quantity, h.AvgBuyPrice))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<TransactionDto>> GetTransactionHistoryAsync(int userId, CancellationToken cancellationToken = default)
    {
        var transactions = await db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return transactions.Select(MapToDto).ToList();
    }

    public async Task<List<LeaderboardDto>> GetLeaderboardAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.Users
            .AsNoTracking()
            .Include(u => u.Holdings)
            .ToListAsync(cancellationToken);

        var leaderboard = new List<LeaderboardDto>();

        foreach (var user in users)
        {
            decimal holdingsValue = user.Holdings?.Sum(h => h.Quantity * h.AvgBuyPrice) ?? 0m;
            decimal totalPortfolioValue = user.VirtualBalance + holdingsValue;
            decimal initialBalance = 10000m;

            decimal profitLossPercent = initialBalance > 0
                ? ((totalPortfolioValue - initialBalance) / initialBalance) * 100m
                : 0m;

            leaderboard.Add(new LeaderboardDto
            {
                Username = user.Username,
                ProfitLossPercentage = Math.Round(profitLossPercent, 2)
            });
        }

        return leaderboard
            .OrderByDescending(x => x.ProfitLossPercentage)
            .ToList();
    }

    public async Task<TransactionDto> BuyAsync(
        int userId,
        string symbol,
        decimal quantity,
        decimal pricePerUnit,
        CancellationToken cancellationToken = default)
    {
        ValidateTradeInputs(symbol, quantity, pricePerUnit);

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var estimatedCost = quantity * pricePerUnit;

        var user = await GetUserOrThrowAsync(userId, cancellationToken);

        if (user.VirtualBalance < estimatedCost)
            throw new InvalidOperationException("Yetersiz bakiye.");

        // 1. Önce Binance Testnet'e gerçek MARKET BUY emri gönder
        JsonElement orderResult;
        try
        {
            orderResult = await binanceClient.CreateOrderAsync(
                symbol: normalizedSymbol,
                side: "BUY",
                type: "MARKET",
                quantity: quantity
            );
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Binance Testnet alış emri başarısız olduğu için işlem iptal edildi: {ex.Message}");
        }

        // 2. Emrin GERÇEK dolum bilgisini oku (istemcinin gönderdiği fiyatı değil)
        var (executedQty, actualPrice, actualCost) =
            ExtractFillInfo(orderResult, quantity, pricePerUnit);

        if (user.VirtualBalance < actualCost)
            throw new InvalidOperationException(
                $"Emir Binance'te {actualCost:F2} USDT tutarında gerçekleşti ancak bakiye yetersiz. " +
                "Testnet ile defter arasında tutarsızlık oluştu, manuel kontrol gerekiyor.");

        // 3. Veritabanını defter olarak güncelle
        user.VirtualBalance -= actualCost;

        var holding = await db.PortfolioHoldings
            .FirstOrDefaultAsync(h => h.UserId == userId && h.Symbol == normalizedSymbol, cancellationToken);

        if (holding == null)
        {
            holding = new PortfolioHolding
            {
                UserId = userId,
                Symbol = normalizedSymbol,
                Quantity = executedQty,
                AvgBuyPrice = actualPrice
            };
            db.PortfolioHoldings.Add(holding);
        }
        else
        {
            var totalQuantity = holding.Quantity + executedQty;
            holding.AvgBuyPrice = ((holding.Quantity * holding.AvgBuyPrice) + (executedQty * actualPrice)) / totalQuantity;
            holding.Quantity = totalQuantity;
        }

        var transaction = new Transaction
        {
            UserId = userId,
            Symbol = normalizedSymbol,
            Type = TransactionType.Buy,
            Quantity = executedQty,
            Price = actualPrice,
            CreatedAt = DateTime.UtcNow
        };
        db.Transactions.Add(transaction);

        await db.SaveChangesAsync(cancellationToken);
        return MapToDto(transaction);
    }

    public async Task<TransactionDto> SellAsync(
        int userId,
        string symbol,
        decimal quantity,
        decimal pricePerUnit,
        CancellationToken cancellationToken = default)
    {
        ValidateTradeInputs(symbol, quantity, pricePerUnit);

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        var user = await GetUserOrThrowAsync(userId, cancellationToken);

        var holding = await db.PortfolioHoldings
            .FirstOrDefaultAsync(h => h.UserId == userId && h.Symbol == normalizedSymbol, cancellationToken);

        if (holding == null || holding.Quantity < quantity)
            throw new InvalidOperationException("Yetersiz coin miktarı.");

        // 1. Önce Binance Testnet'e gerçek MARKET SELL emri gönder
        JsonElement orderResult;
        try
        {
            orderResult = await binanceClient.CreateOrderAsync(
                symbol: normalizedSymbol,
                side: "SELL",
                type: "MARKET",
                quantity: quantity
            );
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Binance Testnet satış emri başarısız olduğu için işlem iptal edildi: {ex.Message}");
        }

        // 2. Emrin GERÇEK dolum bilgisini oku
        var (executedQty, actualPrice, actualProceeds) =
            ExtractFillInfo(orderResult, quantity, pricePerUnit);

        // 3. Veritabanını defter olarak güncelle
        holding.Quantity -= executedQty;
        if (holding.Quantity <= 0)
            db.PortfolioHoldings.Remove(holding);

        user.VirtualBalance += actualProceeds;

        var transaction = new Transaction
        {
            UserId = userId,
            Symbol = normalizedSymbol,
            Type = TransactionType.Sell,
            Quantity = executedQty,
            Price = actualPrice,
            CreatedAt = DateTime.UtcNow
        };
        db.Transactions.Add(transaction);

        await db.SaveChangesAsync(cancellationToken);
        return MapToDto(transaction);
    }

    /// <summary>
    /// Binance emir cevabından gerçekleşen miktarı, ortalama dolum fiyatını ve toplam tutarı çıkarır.
    /// Alanlar okunamazsa istemciden gelen değerlere geri döner.
    /// </summary>
    private static (decimal ExecutedQty, decimal AvgPrice, decimal TotalAmount) ExtractFillInfo(
        JsonElement order,
        decimal fallbackQuantity,
        decimal fallbackPrice)
    {
        var executedQty = ReadDecimal(order, "executedQty");
        var quoteQty = ReadDecimal(order, "cummulativeQuoteQty");

        if (executedQty > 0 && quoteQty > 0)
            return (executedQty, quoteQty / executedQty, quoteQty);

        // Binance beklenen alanları döndürmediyse istemci değerlerine geri dön
        return (fallbackQuantity, fallbackPrice, fallbackQuantity * fallbackPrice);
    }

    private static decimal ReadDecimal(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return 0m;

        if (!element.TryGetProperty(propertyName, out var property))
            return 0m;

        var raw = property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();

        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }

    private async Task<User> GetUserOrThrowAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
            throw new KeyNotFoundException("Kullanıcı bulunamadı.");
        return user;
    }

    private static void ValidateTradeInputs(string symbol, decimal quantity, decimal pricePerUnit)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Sembol boş olamaz.");

        if (quantity <= 0)
            throw new ArgumentException("Miktar sıfırdan büyük olmalıdır.");

        if (pricePerUnit <= 0)
            throw new ArgumentException("Fiyat sıfırdan büyük olmalıdır.");
    }

    private static TransactionDto MapToDto(Transaction t) =>
        new(t.Id, t.Symbol, t.Type, t.Quantity, t.Price, t.Quantity * t.Price, t.CreatedAt);
}