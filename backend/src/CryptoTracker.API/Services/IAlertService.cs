using CryptoTracker.API.DTOs;

namespace CryptoTracker.API.Services;

public interface IAlertService
{
    Task<AlertResponse> CreateAsync(int userId, CreateAlertRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<AlertResponse>> GetByUserAsync(int userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(int userId, int alertId, CancellationToken cancellationToken = default);
    Task<AlertResponse> ToggleAsync(int userId, int alertId, ToggleAlertRequest request, CancellationToken cancellationToken = default);
    Task<AlertSignalsResponse> GetSignalsAsync(int userId, int alertId, CancellationToken cancellationToken = default);
}
