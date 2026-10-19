using CryptoTracker.API.Data;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CryptoTracker.API.Services;

public class PortfolioSnapshotOptions
{
    public const string SectionName = "PortfolioSnapshot";

    /// <summary>
    /// Servisin kaç dakikada bir uyanıp "bu saatin kaydı alındı mı" diye baktığı.
    /// Kayıt yine saatte bir alınır; kısa aralık, uygulama yeniden başlayınca o saatin
    /// kaydının kaçmaması içindir.
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 5;

    /// <summary>Bu kadar günden eski saatlik kayıtlar günde bire indirgenir.</summary>
    public int HourlyRetentionDays { get; set; } = 30;

    /// <summary>Bu kadar günden eski kayıtlar tamamen silinir (son 1 yıl tutulur).</summary>
    public int MaxRetentionDays { get; set; } = 365;
}

/// <summary>
/// Saatte bir tüm kullanıcıların portföy değerini PortfolioSnapshots tablosuna yazar.
/// AlertMonitorService ile aynı desen: zamanlayıcı burada, iş mantığı test edilebilir processor'da.
/// </summary>
public class PortfolioSnapshotService(
    IServiceScopeFactory scopeFactory,
    IOptions<PortfolioSnapshotOptions> options,
    ILogger<PortfolioSnapshotService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Max(1, options.Value.CheckIntervalMinutes);
        logger.LogInformation(
            "PortfolioSnapshotService started. CheckIntervalMinutes={IntervalMinutes}.",
            intervalMinutes);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var binance = scope.ServiceProvider.GetRequiredService<IBinancePriceService>();
                var processor = scope.ServiceProvider.GetRequiredService<IPortfolioSnapshotProcessor>();

                await processor.ProcessAsync(db, binance, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Portfolio snapshot cycle failed; will retry on next interval");
            }
        }

        logger.LogInformation("PortfolioSnapshotService stopping");
    }
}

public interface IPortfolioSnapshotProcessor
{
    Task<PortfolioSnapshotCycleResult> ProcessAsync(
        AppDbContext db,
        IBinancePriceService binance,
        CancellationToken cancellationToken);
}

/// <param name="CreatedCount">Bu turda yazılan kayıt sayısı.</param>
/// <param name="SkippedUserCount">Bu saatin kaydı zaten olduğu için atlanan kullanıcı sayısı.</param>
/// <param name="SymbolCount">Fiyatı istenen farklı coin sayısı.</param>
/// <param name="PriceRequestCount">Binance'e yapılan fiyat çağrısı (0 ya da 1; kullanıcı sayısına bağlı değil).</param>
/// <param name="CompactedCount">Saklama politikası yüzünden silinen eski kayıt sayısı.</param>
public record PortfolioSnapshotCycleResult(
    int CreatedCount,
    int SkippedUserCount,
    int SymbolCount,
    int PriceRequestCount,
    int CompactedCount);

public class PortfolioSnapshotProcessor(
    IClock clock,
    IOptions<PortfolioSnapshotOptions> options,
    ILogger<PortfolioSnapshotProcessor> logger) : IPortfolioSnapshotProcessor
{
    public async Task<PortfolioSnapshotCycleResult> ProcessAsync(
        AppDbContext db,
        IBinancePriceService binance,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var hourStart = StartOfHour(now);

        // Uygulama aynı saat içinde yeniden başlarsa çift kayıt olmasın:
        // bu saat için kaydı olan kullanıcılar atlanır.
        var alreadyTaken = await db.PortfolioSnapshots
            .Where(s => s.TakenAt >= hourStart)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var users = await db.Users
            .AsNoTracking()
            .Where(u => !alreadyTaken.Contains(u.Id))
            .Select(u => new { u.Id, u.VirtualBalance })
            .ToListAsync(cancellationToken);

        var created = 0;
        var symbolCount = 0;
        var priceRequests = 0;

        if (users.Count > 0)
        {
            var userIds = users.Select(u => u.Id).ToList();
            var holdings = await db.PortfolioHoldings
                .AsNoTracking()
                .Where(h => userIds.Contains(h.UserId) && h.Quantity > 0)
                .ToListAsync(cancellationToken);

            var symbols = holdings
                .Select(h => NormalizeSymbol(h.Symbol))
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            symbolCount = symbols.Count;

            IReadOnlyDictionary<string, decimal> prices = new Dictionary<string, decimal>(StringComparer.Ordinal);
            if (symbols.Count > 0)
            {
                // Tüm kullanıcıların coinleri için TEK çağrı; kullanıcı başına istek atılmaz.
                prices = await binance.GetPricesAsync(symbols, cancellationToken);
                priceRequests = 1;

                if (prices.Count == 0)
                {
                    // Binance'e ulaşılamadı: coinleri 0 sayıp grafikte sahte bir düşüş yazmak yerine
                    // bu turu atla; bir sonraki kontrolde (aynı saat içinde) tekrar denenir.
                    logger.LogWarning(
                        "Portfolio snapshot skipped: no prices returned for {SymbolCount} symbols",
                        symbols.Count);
                    return new PortfolioSnapshotCycleResult(0, alreadyTaken.Count, symbols.Count, priceRequests, 0);
                }
            }

            var holdingsByUser = holdings.ToLookup(h => h.UserId);
            foreach (var user in users)
            {
                db.PortfolioSnapshots.Add(BuildSnapshot(
                    user.Id, user.VirtualBalance, holdingsByUser[user.Id], prices, now));
                created++;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        var compacted = await CompactOldSnapshotsAsync(db, now, cancellationToken);

        logger.LogInformation(
            "Portfolio snapshot cycle complete: Created={Created}, AlreadyTakenThisHour={Skipped}, Symbols={SymbolCount}, BinancePriceRequests={PriceRequests}, Compacted={Compacted}",
            created,
            alreadyTaken.Count,
            symbolCount,
            priceRequests,
            compacted);

        return new PortfolioSnapshotCycleResult(created, alreadyTaken.Count, symbolCount, priceRequests, compacted);
    }

    /// <summary>
    /// Portföy sayfasındaki hesapla aynı: nakit bakiye + her coin için miktar × güncel fiyat.
    /// Binance bir coinin fiyatını döndürmediyse (örn. listeden kalkmış) o coin ortalama alış
    /// fiyatıyla sayılır; 0 saymak grafikte gerçek olmayan bir çöküş gösterirdi.
    /// </summary>
    internal static PortfolioSnapshot BuildSnapshot(
        int userId,
        decimal cashBalance,
        IEnumerable<PortfolioHolding> holdings,
        IReadOnlyDictionary<string, decimal> prices,
        DateTime takenAt)
    {
        var holdingsValue = 0m;
        foreach (var holding in holdings)
        {
            var price = prices.TryGetValue(NormalizeSymbol(holding.Symbol), out var current)
                ? current
                : holding.AvgBuyPrice;
            holdingsValue += holding.Quantity * price;
        }

        holdingsValue = Math.Round(holdingsValue, 8);

        return new PortfolioSnapshot
        {
            UserId = userId,
            TakenAt = takenAt,
            CashBalance = cashBalance,
            HoldingsValue = holdingsValue,
            TotalValue = cashBalance + holdingsValue
        };
    }

    /// <summary>
    /// Saklama politikası:
    /// - Son HourlyRetentionDays (30) gün saatlik kalır.
    /// - Daha eski günlerde her kullanıcı için yalnızca o günün son kaydı kalır.
    /// - MaxRetentionDays (365) günden eski kayıtlar silinir.
    /// Sınırlar gün başına yuvarlanır, böylece bir gün yarım indirgenmez.
    /// </summary>
    internal async Task<int> CompactOldSnapshotsAsync(
        AppDbContext db,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var retentionDays = Math.Max(1, options.Value.HourlyRetentionDays);
        var maxRetentionDays = Math.Max(retentionDays, options.Value.MaxRetentionDays);
        var cutoff = now.Date.AddDays(-retentionDays);
        var expiredBefore = now.Date.AddDays(-maxRetentionDays);

        var expired = await db.PortfolioSnapshots
            .Where(s => s.TakenAt < expiredBefore)
            .ToListAsync(cancellationToken);

        if (expired.Count > 0)
        {
            db.PortfolioSnapshots.RemoveRange(expired);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Önce birden fazla kaydı olan eski günleri bul; normalde her saat en fazla bir gün çıkar.
        var crowdedDays = await db.PortfolioSnapshots
            .Where(s => s.TakenAt < cutoff)
            .GroupBy(s => new { s.UserId, Day = s.TakenAt.Date })
            .Where(g => g.Count() > 1)
            .Select(g => new { g.Key.UserId, g.Key.Day })
            .ToListAsync(cancellationToken);

        if (crowdedDays.Count == 0)
            return expired.Count;

        var userIds = crowdedDays.Select(d => d.UserId).Distinct().ToList();
        var firstDay = crowdedDays.Min(d => d.Day);
        var crowdedSet = crowdedDays.Select(d => (d.UserId, d.Day)).ToHashSet();

        var candidates = await db.PortfolioSnapshots
            .Where(s => s.TakenAt >= firstDay && s.TakenAt < cutoff && userIds.Contains(s.UserId))
            .ToListAsync(cancellationToken);

        var toDelete = candidates
            .Where(s => crowdedSet.Contains((s.UserId, s.TakenAt.Date)))
            .GroupBy(s => (s.UserId, s.TakenAt.Date))
            .SelectMany(g => g
                .OrderByDescending(s => s.TakenAt)
                .ThenByDescending(s => s.Id)
                .Skip(1))
            .ToList();

        if (toDelete.Count == 0)
            return expired.Count;

        db.PortfolioSnapshots.RemoveRange(toDelete);
        await db.SaveChangesAsync(cancellationToken);
        return expired.Count + toDelete.Count;
    }

    internal static DateTime StartOfHour(DateTime utc) =>
        new(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);

    private static string NormalizeSymbol(string symbol) =>
        (symbol ?? string.Empty).Trim().ToUpperInvariant();
}
