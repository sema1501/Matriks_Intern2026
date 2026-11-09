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
    IReadOnlyList<BacktestSignalDto> Signals,
    BacktestMetricsDto Metrics,
    // Yalnızca botta stop-loss / take-profit varsa dolu: aynı mumlarda limitsiz koşunun metrikleri.
    BacktestMetricsDto? MetricsWithoutRiskLimits = null
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

public record BacktestMetricsDto(
    decimal? MaxDrawdownPercent,
    decimal? WinRatePercent,
    decimal? ProfitFactor,
    decimal? AverageTradeReturnPercent,
    int StopLossTrades,
    int TakeProfitTrades,
    decimal? StrategyReturnPercent,
    decimal? BuyAndHoldReturnPercent,
    decimal? BuyAndHoldDifferencePercent,
    IReadOnlyList<BacktestEquityPointDto> EquityCurve
);

public record BacktestEquityPointDto(
    DateTime Timestamp,
    decimal Value
);

public record BacktestSignalDto(
    DateTime Timestamp,
    string Type,
    decimal Price,
    decimal? Rsi,
    string Reason = "Strategy"
);
