namespace CryptoTracker.API.Models;

public class AuditLog
{
    public int Id { get; set; }

    public int ActorUserId { get; set; }
    public User ActorUser { get; set; } = null!;

    public string Action { get; set; } = string.Empty;

    /// <summary>Target entity id (bot id for bot administration actions).</summary>
    public int TargetId { get; set; }

    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class AuditLogActions
{
    public const string BotForceStopped = "BotForceStopped";
    public const string BotFlagged = "BotFlagged";
}
