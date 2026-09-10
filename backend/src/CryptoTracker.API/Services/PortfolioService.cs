using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class PortfolioService(AppDbContext db) : IPortfolioService
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

    public async Task<PagedResult<TransactionDto>> GetTransactionHistoryAsync(
        int userId,
        int pageNumber = 1,
        int pageSize = 20,
        string? symbol = null,
        TransactionType? type = null,
        CancellationToken cancellationToken = default)
    {
        // Sayfa parametrelerini güvenli aralığa çek (Görev 51).
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId);

        // Opsiyonel filtreler — sembolde kısmi arama ("eth" → ETHUSDT bulunur) (Görev 51)
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var normalized = symbol.Trim().ToUpperInvariant();
            query = query.Where(t => t.Symbol.Contains(normalized));
        }

        if (type is not null)
            query = query.Where(t => t.Type == type);

        var totalCount = await query.CountAsync(cancellationToken);

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<TransactionDto>(
            transactions.Select(MapToDto).ToList(),
            totalCount,
            pageNumber,
            pageSize,
            totalPages);
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
        var totalCost = quantity * pricePerUnit;

        var user = await GetUserOrThrowAsync(userId, cancellationToken);

        if (user.VirtualBalance < totalCost)
            throw new InvalidOperationException("Yetersiz bakiye.");

        user.VirtualBalance -= totalCost;

        var holding = await db.PortfolioHoldings
            .FirstOrDefaultAsync(h => h.UserId == userId && h.Symbol == normalizedSymbol, cancellationToken);

        if (holding == null)
        {
            holding = new PortfolioHolding
            {
                UserId = userId,
                Symbol = normalizedSymbol,
                Quantity = quantity,
                AvgBuyPrice = pricePerUnit
            };
            db.PortfolioHoldings.Add(holding);
        }
        else
        {
            var totalQuantity = holding.Quantity + quantity;
            holding.AvgBuyPrice = ((holding.Quantity * holding.AvgBuyPrice) + (quantity * pricePerUnit)) / totalQuantity;
            holding.Quantity = totalQuantity;
        }

        var transaction = new Transaction
        {
            UserId = userId,
            Symbol = normalizedSymbol,
            Type = TransactionType.Buy,
            Quantity = quantity,
            Price = pricePerUnit,
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
        var totalProceeds = quantity * pricePerUnit;

        var user = await GetUserOrThrowAsync(userId, cancellationToken);

        var holding = await db.PortfolioHoldings
            .FirstOrDefaultAsync(h => h.UserId == userId && h.Symbol == normalizedSymbol, cancellationToken);

        if (holding == null || holding.Quantity < quantity)
            throw new InvalidOperationException("Yetersiz coin miktarı.");

        holding.Quantity -= quantity;
        if (holding.Quantity <= 0)
            db.PortfolioHoldings.Remove(holding);

        user.VirtualBalance += totalProceeds;

        var transaction = new Transaction
        {
            UserId = userId,
            Symbol = normalizedSymbol,
            Type = TransactionType.Sell,
            Quantity = quantity,
            Price = pricePerUnit,
            CreatedAt = DateTime.UtcNow
        };
        db.Transactions.Add(transaction);

        await db.SaveChangesAsync(cancellationToken);
        return MapToDto(transaction);
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