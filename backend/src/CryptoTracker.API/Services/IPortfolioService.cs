using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;

namespace CryptoTracker.API.Services;

public interface IPortfolioService
{
    Task<decimal> GetBalanceAsync(int userId, CancellationToken cancellationToken = default);
    Task<List<HoldingDto>> GetHoldingsAsync(int userId, CancellationToken cancellationToken = default);
    Task<PagedResult<TransactionDto>> GetTransactionHistoryAsync(
        int userId,
        int pageNumber = 1,
        int pageSize = 20,
        string? symbol = null,
        TransactionType? type = null,
        CancellationToken cancellationToken = default);
    Task<List<LeaderboardDto>> GetLeaderboardAsync(CancellationToken cancellationToken = default);
    Task<TransactionDto> BuyAsync(int userId, string symbol, decimal quantity, decimal pricePerUnit, CancellationToken cancellationToken = default);
    Task<TransactionDto> SellAsync(int userId, string symbol, decimal quantity, decimal pricePerUnit, CancellationToken cancellationToken = default);
}