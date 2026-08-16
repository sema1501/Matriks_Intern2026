namespace CryptoTracker.API.Services;

public interface IAdminBotService
{
    Task ForceStopAsync(
        int actorUserId,
        int botId,
        string? adminNote,
        CancellationToken cancellationToken = default);

    Task FlagAsync(
        int actorUserId,
        int botId,
        string? adminNote,
        CancellationToken cancellationToken = default);
}
