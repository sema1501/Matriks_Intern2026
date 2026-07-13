using CryptoTracker.API.DTOs;

namespace CryptoTracker.API.Services;

public interface IAlertService
{
    Task<AlertResponse> CreateAsync(int userId, CreateAlertRequest request);
    Task<IEnumerable<AlertResponse>> GetByUserAsync(int userId);
    Task DeleteAsync(int userId, int alertId);
}
