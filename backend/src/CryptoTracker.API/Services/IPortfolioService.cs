using CryptoTracker.API.DTOs;

namespace CryptoTracker.API.Services;

public interface IPortfolioService
{
    Task<decimal> GetBalanceAsync(int userId, CancellationToken cancellationToken = default);
    Task<List<HoldingDto>> GetHoldingsAsync(int userId, CancellationToken cancellationToken = default);
    Task<List<TransactionDto>> GetTransactionHistoryAsync(int userId, CancellationToken cancellationToken = default);
    Task<TransactionDto> BuyAsync(int userId, string symbol, decimal quantity, decimal pricePerUnit, CancellationToken cancellationToken = default);
    Task<TransactionDto> SellAsync(int userId, string symbol, decimal quantity, decimal pricePerUnit, CancellationToken cancellationToken = default);
}
