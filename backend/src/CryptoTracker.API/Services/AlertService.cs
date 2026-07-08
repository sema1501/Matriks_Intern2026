using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class AlertService(AppDbContext db) : IAlertService
{
    public async Task<AlertResponse> CreateAsync(int userId, CreateAlertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            throw new ArgumentException("Sembol boş olamaz.");

        if (request.TargetPrice <= 0)
            throw new ArgumentException("Hedef fiyat sıfırdan büyük olmalıdır.");

        if (!Enum.IsDefined(typeof(AlertDirection), request.Direction))
            throw new ArgumentException("Geçersiz alarm yönü. Above veya Below olmalıdır.");

        var alert = new PriceAlert
        {
            UserId      = userId,
            Symbol      = request.Symbol.Trim().ToUpperInvariant(),
            TargetPrice = request.TargetPrice,
            Direction   = request.Direction,
            IsTriggered = false,
            CreatedAt   = DateTime.UtcNow
        };

        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync();

        return MapToResponse(alert);
    }

    public async Task<IEnumerable<AlertResponse>> GetByUserAsync(int userId)
    {
        var alerts = await db.PriceAlerts
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return alerts.Select(MapToResponse);
    }

    public async Task DeleteAsync(int userId, int alertId)
    {
        var alert = await db.PriceAlerts
            .FirstOrDefaultAsync(a => a.Id == alertId && a.UserId == userId);

        if (alert == null)
            throw new KeyNotFoundException("Alarm bulunamadı.");

        db.PriceAlerts.Remove(alert);
        await db.SaveChangesAsync();
    }

    private static AlertResponse MapToResponse(PriceAlert alert) =>
        new(alert.Id, alert.Symbol, alert.TargetPrice, alert.Direction, alert.IsTriggered, alert.CreatedAt);
}
