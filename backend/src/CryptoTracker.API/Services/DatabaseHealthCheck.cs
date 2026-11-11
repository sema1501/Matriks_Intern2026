using CryptoTracker.API.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CryptoTracker.API.Services;

/// <summary>/health ucu için: API ayakta olsa bile veritabanına ulaşamıyorsa sağlıklı sayılmaz.</summary>
public class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
        => await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Veritabanına bağlanılamıyor.");
}
