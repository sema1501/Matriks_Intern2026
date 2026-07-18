using CryptoTracker.API.Models;

namespace CryptoTracker.API.DTOs;

public record CreateAlertRequest(
    string Symbol,
    decimal TargetPrice,
    AlertDirection Direction,
    AlertInterval Interval = AlertInterval.Minute
);

public record ToggleAlertRequest(bool IsActive);

public record AlertResponse(
    int Id,
    string Symbol,
    decimal TargetPrice,
    AlertDirection Direction,
    bool IsTriggered,
    bool IsActive,
    AlertInterval Interval,
    int SignalCount,
    DateTime? LastTriggeredAt,
    DateTime CreatedAt
);

public record AlertSignalDto(
    int Id,
    int AlertId,
    decimal PriceAtTrigger,
    DateTime TriggeredAt
);

public record AlertSignalsResponse(
    int AlertId,
    int TotalCount,
    DateTime? LastTriggeredAt,
    IReadOnlyList<AlertSignalDto> Signals
);
