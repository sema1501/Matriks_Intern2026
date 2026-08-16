using CryptoTracker.API.Data;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class AdminBotService(AppDbContext db, IClock clock) : IAdminBotService
{
    private const int MaxNoteLength = 500;
    private const int MaxDetailsLength = 2000;

    public async Task ForceStopAsync(
        int actorUserId,
        int botId,
        string? adminNote,
        CancellationToken cancellationToken = default)
    {
        var bot = await db.TradingBots
            .FirstOrDefaultAsync(b => b.Id == botId, cancellationToken);

        if (bot is null)
            throw new KeyNotFoundException("Bot bulunamadı.");

        bot.IsActive = false;

        db.AuditLogs.Add(CreateLog(
            actorUserId,
            AuditLogActions.BotForceStopped,
            bot,
            adminNote,
            "Bot zorla durduruldu."));

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FlagAsync(
        int actorUserId,
        int botId,
        string? adminNote,
        CancellationToken cancellationToken = default)
    {
        var bot = await db.TradingBots
            .FirstOrDefaultAsync(b => b.Id == botId, cancellationToken);

        if (bot is null)
            throw new KeyNotFoundException("Bot bulunamadı.");

        bot.IsFlagged = true;

        db.AuditLogs.Add(CreateLog(
            actorUserId,
            AuditLogActions.BotFlagged,
            bot,
            adminNote,
            "Bot şüpheli olarak işaretlendi."));

        await db.SaveChangesAsync(cancellationToken);
    }

    private AuditLog CreateLog(
        int actorUserId,
        string action,
        TradingBot bot,
        string? adminNote,
        string summary)
    {
        var note = SanitizeNote(adminNote);
        var details = $"{summary} BotId={bot.Id}; Symbol={bot.Symbol}; OwnerUserId={bot.UserId}.";
        if (note is not null)
            details += $" Note={note}";

        if (details.Length > MaxDetailsLength)
            details = details[..MaxDetailsLength];

        return new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            TargetId = bot.Id,
            Details = details,
            CreatedAt = clock.UtcNow
        };
    }

    private static string? SanitizeNote(string? adminNote)
    {
        if (string.IsNullOrWhiteSpace(adminNote))
            return null;

        var trimmed = adminNote.Trim();
        return trimmed.Length <= MaxNoteLength ? trimmed : trimmed[..MaxNoteLength];
    }
}
