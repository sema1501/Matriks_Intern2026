using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public interface IPortfolioHistoryService
{
    /// <summary>Kullanıcının kendi portföy değer geçmişi. range: 24h, 7d, 30d, all.</summary>
    Task<PortfolioHistoryResponse> GetHistoryAsync(
        int userId,
        string? range,
        CancellationToken cancellationToken = default);
}

public class PortfolioHistoryService(AppDbContext db, IClock clock) : IPortfolioHistoryService
{
    public const string DefaultRange = "24h";

    public async Task<PortfolioHistoryResponse> GetHistoryAsync(
        int userId,
        string? range,
        CancellationToken cancellationToken = default)
    {
        var normalizedRange = string.IsNullOrWhiteSpace(range)
            ? DefaultRange
            : range.Trim().ToLowerInvariant();

        var now = clock.UtcNow;
        DateTime? from = normalizedRange switch
        {
            "24h" => now.AddHours(-24),
            "7d" => now.AddDays(-7),
            "30d" => now.AddDays(-30),
            "all" => null,
            _ => throw new ArgumentException("Geçersiz aralık. Kullanılabilir değerler: 24h, 7d, 30d, all.")
        };

        var query = db.PortfolioSnapshots
            .AsNoTracking()
            .Where(s => s.UserId == userId);

        if (from is not null)
            query = query.Where(s => s.TakenAt >= from.Value);

        var points = await query
            .OrderBy(s => s.TakenAt)
            .ThenBy(s => s.Id)
            .Select(s => new PortfolioHistoryPointDto(s.TakenAt, s.CashBalance, s.HoldingsValue, s.TotalValue))
            .ToListAsync(cancellationToken);

        decimal? changeUsd = null;
        decimal? changePercent = null;
        if (points.Count >= 2)
        {
            var first = points[0].TotalValue;
            changeUsd = points[^1].TotalValue - first;
            changePercent = first == 0m ? null : Math.Round(changeUsd.Value / first * 100m, 2);
        }

        return new PortfolioHistoryResponse(normalizedRange, points, changeUsd, changePercent);
    }
}
