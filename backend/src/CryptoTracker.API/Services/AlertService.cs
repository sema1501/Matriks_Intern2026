using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class AlertService(AppDbContext db) : IAlertService
{
    public async Task<AlertResponse> CreateAsync(int userId, CreateAlertRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            throw new ArgumentException("Sembol boş olamaz.");

        if (request.TargetPrice <= 0)
            throw new ArgumentException("Hedef fiyat sıfırdan büyük olmalıdır.");

        if (!Enum.IsDefined(typeof(AlertDirection), request.Direction))
            throw new ArgumentException("Geçersiz alarm yönü. Above veya Below olmalıdır.");

        if (!Enum.IsDefined(typeof(AlertInterval), request.Interval))
            throw new ArgumentException("Geçersiz alarm aralığı.");

        if (!AlertConditionEvaluator.IsIntervalSupported(request.Interval))
            throw new ArgumentException(AlertConditionEvaluator.UnsupportedIntervalMessage);

        var alert = new PriceAlert
        {
            UserId      = userId,
            Symbol      = request.Symbol.Trim().ToUpperInvariant(),
            TargetPrice = request.TargetPrice,
            Direction   = request.Direction,
            Interval    = request.Interval,
            IsActive    = true,
            IsTriggered = false,
            CreatedAt   = DateTime.UtcNow
        };

        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync(cancellationToken);

        return MapToResponse(alert, signalCount: 0, lastTriggeredAt: null);
    }

    public async Task<IEnumerable<AlertResponse>> GetByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var alerts = await db.PriceAlerts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                Alert = a,
                SignalCount = a.Signals.Count(),
                LastTriggeredAt = a.Signals
                    .OrderByDescending(s => s.TriggeredAt)
                    .Select(s => (DateTime?)s.TriggeredAt)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return alerts.Select(x => MapToResponse(x.Alert, x.SignalCount, x.LastTriggeredAt));
    }

    public async Task DeleteAsync(int userId, int alertId, CancellationToken cancellationToken = default)
    {
        var alert = await db.PriceAlerts
            .FirstOrDefaultAsync(a => a.Id == alertId && a.UserId == userId, cancellationToken);

        if (alert == null)
            throw new KeyNotFoundException("Alarm bulunamadı.");

        db.PriceAlerts.Remove(alert);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AlertResponse> ToggleAsync(int userId, int alertId, ToggleAlertRequest request, CancellationToken cancellationToken = default)
    {
        var alert = await db.PriceAlerts
            .FirstOrDefaultAsync(a => a.Id == alertId && a.UserId == userId, cancellationToken);

        if (alert == null)
            throw new KeyNotFoundException("Alarm bulunamadı.");

        alert.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        var signalCount = await db.AlertSignals
            .CountAsync(s => s.AlertId == alert.Id, cancellationToken);

        var lastTriggeredAt = await db.AlertSignals
            .Where(s => s.AlertId == alert.Id)
            .OrderByDescending(s => s.TriggeredAt)
            .Select(s => (DateTime?)s.TriggeredAt)
            .FirstOrDefaultAsync(cancellationToken);

        return MapToResponse(alert, signalCount, lastTriggeredAt);
    }

    public async Task<AlertSignalsResponse> GetSignalsAsync(int userId, int alertId, CancellationToken cancellationToken = default)
    {
        var alertExists = await db.PriceAlerts
            .AsNoTracking()
            .AnyAsync(a => a.Id == alertId && a.UserId == userId, cancellationToken);

        if (!alertExists)
            throw new KeyNotFoundException("Alarm bulunamadı.");

        var signals = await db.AlertSignals
            .AsNoTracking()
            .Where(s => s.AlertId == alertId)
            .OrderByDescending(s => s.TriggeredAt)
            .Select(s => new AlertSignalDto(s.Id, s.AlertId, s.PriceAtTrigger, s.TriggeredAt))
            .ToListAsync(cancellationToken);

        return new AlertSignalsResponse(
            alertId,
            signals.Count,
            signals.Count > 0 ? signals[0].TriggeredAt : null,
            signals
        );
    }

    private static AlertResponse MapToResponse(PriceAlert alert, int signalCount, DateTime? lastTriggeredAt) =>
        new(
            alert.Id,
            alert.Symbol,
            alert.TargetPrice,
            alert.Direction,
            alert.IsTriggered,
            alert.IsActive,
            alert.Interval,
            signalCount,
            lastTriggeredAt,
            alert.CreatedAt
        );
}
