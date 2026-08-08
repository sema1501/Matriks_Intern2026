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
    int FailedSignals,
    double ApprovalRate,
    decimal BotProfitLoss,
    List<BotActivePosition> ActivePositions
);

public record BotActivePosition(
    string Symbol, 
    decimal Quantity, 
    decimal TotalCost
);

/// <summary>
/// DEVELOPMENT / DEBUG ONLY — smoke-test automatic virtual portfolio execution.
/// </summary>
public record DebugBotExecuteRequest(string SignalType);

/// <summary>
/// DEVELOPMENT / DEBUG ONLY response for forced BUY/SELL smoke execution.
/// </summary>
public record DebugBotExecuteResponse(
    string Message,
    int BotId,
    bool BotIsActive,
    BotSignalResponse Signal
);

/// <summary>
/// DEVELOPMENT / DEBUG ONLY — prove zone-entry decision + automatic execution path.
/// </summary>
public record DebugZoneEntryRequest(decimal PreviousRsi, decimal CurrentRsi);

/// <summary>
/// DEVELOPMENT / DEBUG ONLY response for zone-entry runtime proof.
/// </summary>
public record DebugZoneEntryResponse(
    string Message,
    bool SignalDetected,
    BotSignalType? SignalType,
    int BotId,
    bool BotIsActive,
    decimal BuyRsiThreshold,
    decimal SellRsiThreshold,
    decimal PreviousRsi,
    decimal CurrentRsi,
    BotSignalResponse? Signal
);

