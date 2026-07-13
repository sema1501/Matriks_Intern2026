using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class WatchlistService(AppDbContext db) : IWatchlistService
{
    public async Task<IEnumerable<WatchlistItemDto>> GetByUserAsync(int userId)
    {
        var items = await db.WatchlistItems
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        return items.Select(w => new WatchlistItemDto(w.Id, w.Symbol, w.CreatedAt));
    }

    public async Task<WatchlistItemDto> AddAsync(int userId, string symbol)
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        // Aynı kullanıcı aynı symbol'ü iki kez ekleyemesin
        var exists = await db.WatchlistItems
            .AnyAsync(w => w.UserId == userId && w.Symbol == normalizedSymbol);

        if (exists)
            throw new Exception($"'{normalizedSymbol}' zaten izleme listenizde mevcut.");

        var item = new WatchlistItem
        {
            UserId    = userId,
            Symbol    = normalizedSymbol,
            CreatedAt = DateTime.UtcNow
        };

        db.WatchlistItems.Add(item);
        await db.SaveChangesAsync();

        return new WatchlistItemDto(item.Id, item.Symbol, item.CreatedAt);
    }

    public async Task RemoveAsync(int userId, string symbol)
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        var item = await db.WatchlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.Symbol == normalizedSymbol);

        if (item == null)
            throw new Exception($"'{normalizedSymbol}' izleme listenizde bulunamadı.");

        db.WatchlistItems.Remove(item);
        await db.SaveChangesAsync();
    }
}
