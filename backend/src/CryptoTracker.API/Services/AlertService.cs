using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class AlertService(AppDbContext db, IBinancePriceService? priceService = null) : IAlertService
{
    public async Task<AlertResponse> CreateAsync(int userId, CreateAlertRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            throw new ArgumentException("Sembol boş olamaz.");

        if (!Enum.IsDefined(typeof(AlertDirection), request.Direction))
            throw new ArgumentException("Geçersiz alarm yönü. Above veya Below olmalıdır.");

        if (!Enum.IsDefined(typeof(AlertType), request.Type))
            throw new ArgumentException("Geçersiz alarm tipi.");

        if (!Enum.IsDefined(typeof(AlertInterval), request.Interval))
            throw new ArgumentException("Geçersiz alarm aralığı.");

        if (!AlertConditionEvaluator.IsIntervalSupported(request.Interval))
            throw new ArgumentException(AlertConditionEvaluator.UnsupportedIntervalMessage);

        var symbol = request.Symbol.Trim().ToUpperInvariant();

        var alert = new PriceAlert
        {
            UserId      = userId,
            Symbol      = symbol,
            Direction   = request.Direction,
            Interval    = request.Interval,
            Type        = request.Type,
            IsActive    = true,
            IsTriggered = false,
            CreatedAt   = DateTime.UtcNow
        };

        if (request.Type == AlertType.PercentChange)
        {
            // Yüzde-değişim alarmı: eşik zorunlu, referans fiyat şu anki fiyattan alınır (Görev 46).
            if (request.PercentChangeThreshold is not { } threshold || threshold <= 0)
                throw new ArgumentException("Yüzde değişim eşiği sıfırdan büyük olmalıdır.");

            alert.PercentChangeThreshold = threshold;
            alert.ReferencePrice = await GetCurrentPriceAsync(symbol, cancellationToken);
        }
        else
        {
            // Sabit fiyat alarmı: mevcut davranış.
            if (request.TargetPrice <= 0)
                throw new ArgumentException("Hedef fiyat sıfırdan büyük olmalıdır.");

            alert.TargetPrice = request.TargetPrice;
        }

        db.PriceAlerts.Add(alert);
        await db.SaveChangesAsync(cancellationToken);

        return MapToResponse(alert, signalCount: 0, lastTriggeredAt: null);
    }

    private async Task<decimal> GetCurrentPriceAsync(string symbol, CancellationToken cancellationToken)
    {
        if (priceService is null)
            throw new InvalidOperationException("Fiyat servisi kullanılamıyor; yüzde alarmı için referans fiyat alınamadı.");

        IReadOnlyDictionary<string, decimal> prices;
        try
        {
            prices = await priceService.GetPricesAsync(new[] { symbol }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Geçersiz sembol veya geçici erişim hatası → 500 yerine anlaşılır 400 döndürülür (Görev 46).
            throw new ArgumentException($"'{symbol}' için güncel fiyat alınamadı. Geçerli bir sembol girin (örn. BTCUSDT) veya daha sonra tekrar deneyin.");
        }

        if (!prices.TryGetValue(symbol, out var price) || price <= 0)
            throw new ArgumentException($"'{symbol}' için güncel fiyat alınamadı. Geçerli bir sembol girin (örn. BTCUSDT).");

        return price;
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
            alert.CreatedAt,
            alert.Type,
            alert.PercentChangeThreshold,
            alert.ReferencePrice
        );
}
