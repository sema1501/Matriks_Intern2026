using CryptoTracker.API.DTOs;

namespace CryptoTracker.API.Services;

public interface IWatchlistService
{
    Task<IEnumerable<WatchlistItemDto>> GetByUserAsync(int userId);
    Task<WatchlistItemDto>              AddAsync(int userId, string symbol);
    Task                                RemoveAsync(int userId, string symbol);
}
