using CryptoTracker.API.Models;

namespace CryptoTracker.API.DTOs;

public record CreateAlertRequest(
    string Symbol,
    decimal TargetPrice,
    AlertDirection Direction
);

public record AlertResponse(
    int Id,
    string Symbol,
    decimal TargetPrice,
    AlertDirection Direction,
    bool IsTriggered,
    DateTime CreatedAt
);
