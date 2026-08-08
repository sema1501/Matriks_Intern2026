namespace CryptoTracker.API.DTOs;

public record BacktestRequestDto(
    DateTime StartDate,
    DateTime EndDate
);

public record BacktestResponseDto(
    int BotId,
    string Symbol,
    string Strategy,
    DateTime StartDate,
    DateTime EndDate,
    string Interval,
    BacktestSummaryDto Summary,
    IReadOnlyList<BacktestSignalDto> Signals
);

public record BacktestSummaryDto(
    int TotalSignals,
    int BuySignals,
    int SellSignals,
    int CompletedTrades,
    int WinningTrades,
    int LosingTrades,
    decimal RealizedProfitLoss,
    decimal UnrealizedProfitLoss,
    decimal NetProfitLoss,
    decimal RealizedReturnPercentage
);

public record BacktestSignalDto(
    DateTime Timestamp,
    string Type,
    decimal Price,
    decimal? Rsi
);
