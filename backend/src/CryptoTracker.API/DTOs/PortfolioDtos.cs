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
