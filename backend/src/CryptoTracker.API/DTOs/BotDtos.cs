using CryptoTracker.API.Models;

namespace CryptoTracker.API.DTOs;

public record CreateBotRequest(
    string Symbol,
    decimal BuyRsiThreshold,
    decimal SellRsiThreshold,
    decimal TradeQuantity
);

public record BotResponse(
    int Id,
    string Symbol,
    bool IsActive,
    decimal BuyRsiThreshold,
    decimal SellRsiThreshold,
    decimal TradeQuantity,
    DateTime CreatedAt
);

public record BotSignalResponse(
    int Id,
    int BotId,
    BotSignalType SignalType,
    decimal RsiValueAtSignal,
    decimal PriceAtSignal,
    DateTime CreatedAt,
    BotSignalStatus Status
);

public record SignalActionResponse(
    int SignalId,
    BotSignalStatus Status,
    TransactionDto? Transaction
);

public record BotPerformanceDto(
    int TotalSignals,
    int ApprovedSignals,
    int RejectedSignals,
    int ExpiredSignals,
    double ApprovalRate,
    decimal BotProfitLoss,
    List<BotActivePosition> ActivePositions
);

public record BotActivePosition(
    string Symbol, 
    decimal Quantity, 
    decimal TotalCost
);

