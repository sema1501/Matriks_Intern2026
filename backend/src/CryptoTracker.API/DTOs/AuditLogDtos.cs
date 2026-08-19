namespace CryptoTracker.API.DTOs;

public record AuditLogResponse(
    int Id,
    int ActorUserId,
    string ActorUsername,
    string Action,
    int TargetId,
    string? Details,
    DateTime CreatedAt
);

public record AdminBotActionRequest(string? AdminNote);
