using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class AuditLogService(AppDbContext db) : IAuditLogService
{
    public const int MaxResults = 200;

    public async Task<IReadOnlyList<AuditLogResponse>> GetAsync(
        DateTime? from,
        DateTime? to,
        bool ascending,
        CancellationToken cancellationToken = default)
    {
        var fromUtc = NormalizeFilterInstant(from, isEndExclusiveBound: false);
        var toUtc = NormalizeFilterInstant(to, isEndExclusiveBound: true);

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
            throw new ArgumentException("Başlangıç tarihi bitiş tarihinden sonra olamaz.");

        var query = db.AuditLogs
            .AsNoTracking()
            .Include(a => a.ActorUser)
            .AsQueryable();

        if (fromUtc.HasValue)
            query = query.Where(a => a.CreatedAt >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(a => a.CreatedAt <= toUtc.Value);

        query = ascending
            ? query.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            : query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id);

        var rows = await query
            .Take(MaxResults)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    /// <summary>
    /// Query values without timezone are treated as UTC.
    /// A date-only <paramref name="isEndExclusiveBound"/> bound (midnight) is expanded
    /// to the end of that UTC day so <c>?to=2026-08-15</c> includes the whole day.
    /// </summary>
    private static DateTime? NormalizeFilterInstant(DateTime? value, bool isEndExclusiveBound)
    {
        if (value is null)
            return null;

        var utc = value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };

        if (isEndExclusiveBound && utc.TimeOfDay == TimeSpan.Zero)
            return utc.Date.AddDays(1).AddTicks(-1);

        return utc;
    }

    private static AuditLogResponse Map(AuditLog log) =>
        new(
            log.Id,
            log.ActorUserId,
            log.ActorUser.Username,
            log.Action,
            log.TargetId,
            log.Details,
            DateTime.SpecifyKind(log.CreatedAt, DateTimeKind.Utc));
}
