using CryptoTracker.API.Models;

namespace CryptoTracker.API.DTOs;

public record TradeRequest(string Symbol, decimal Quantity, decimal Price);

public record HoldingDto(string Symbol, decimal Quantity, decimal AvgBuyPrice);

public record TransactionDto(
    int Id,
    string Symbol,
    TransactionType Type,
    decimal Quantity,
    decimal Price,
    decimal TotalAmount,
    DateTime CreatedAt
);

public record BalanceDto(decimal Balance);

public record PortfolioHistoryPointDto(
    DateTime TakenAt,
    decimal CashBalance,
    decimal HoldingsValue,
    decimal TotalValue);

/// <param name="Range">İstenen aralık: 24h, 7d, 30d veya all.</param>
/// <param name="ChangeUsd">Aralıktaki ilk ve son kayıt arasındaki fark; 2'den az kayıt varsa null.</param>
/// <param name="ChangePercent">Aynı farkın ilk kayda göre yüzdesi; 2'den az kayıt varsa null.</param>
public record PortfolioHistoryResponse(
    string Range,
    IReadOnlyList<PortfolioHistoryPointDto> Points,
    decimal? ChangeUsd,
    decimal? ChangePercent);
