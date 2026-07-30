using CryptoTracker.API.DTOs;

namespace CryptoTracker.API.Services;

public interface IBotService
{
    Task<List<BotResponse>> GetBotsByUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<BotResponse> CreateBotAsync(int userId, CreateBotRequest request, CancellationToken cancellationToken = default);
    Task<BotResponse> ToggleBotAsync(int userId, int botId, CancellationToken cancellationToken = default);
    Task<List<BotSignalResponse>> GetSignalsAsync(int userId, int botId, CancellationToken cancellationToken = default);
    Task<SignalActionResponse> ApproveSignalAsync(int userId, int signalId, CancellationToken cancellationToken = default);
    Task<SignalActionResponse> RejectSignalAsync(int userId, int signalId, CancellationToken cancellationToken = default);
    Task<BotPerformanceDto> GetBotPerformanceAsync(int userId, CancellationToken cancellationToken = default);
}
