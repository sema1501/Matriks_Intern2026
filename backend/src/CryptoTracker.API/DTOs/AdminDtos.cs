namespace CryptoTracker.API.DTOs;

public record AdminBotDto(
    int Id,
    int UserId,
    string Username,
    string Symbol,
    string Strategy,
    bool IsActive,
    decimal BuyRsiThreshold,
    decimal SellRsiThreshold,
    decimal TradeQuantity,
    DateTime CreatedAt
);

public record AdminPortfolioDto(
    int UserId,
    string Username,
    decimal VirtualBalance,
    List<AdminHoldingDto> Holdings
);

public record AdminHoldingDto(
    int Id,
    string Symbol,
    decimal Quantity,
    decimal AvgBuyPrice
);