using CryptoTracker.API.DTOs;

namespace CryptoTracker.API.Services;

public interface IAuditLogService
{
    Task<IReadOnlyList<AuditLogResponse>> GetAsync(
        DateTime? from,
        DateTime? to,
        bool ascending,
        CancellationToken cancellationToken = default);
}
