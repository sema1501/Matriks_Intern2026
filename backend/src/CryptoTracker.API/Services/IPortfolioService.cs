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
    /// <summary>
    /// CSV dışa aktarma için filtrelenmiş işlem geçmişi (Görev 89).
    /// GetTransactionHistoryAsync ile aynı filtreler, sayfalama yok, en fazla maxRows satır.
    /// </summary>
    Task<List<TransactionDto>> GetTransactionsForExportAsync(
        int userId,
        string? symbol = null,
        TransactionType? type = null,
        int maxRows = 5000,
        CancellationToken cancellationToken = default);
    Task<List<LeaderboardDto>> GetLeaderboardAsync(CancellationToken cancellationToken = default);
    Task<TransactionDto> BuyAsync(int userId, string symbol, decimal quantity, decimal pricePerUnit, CancellationToken cancellationToken = default);
    Task<TransactionDto> SellAsync(int userId, string symbol, decimal quantity, decimal pricePerUnit, CancellationToken cancellationToken = default);
}