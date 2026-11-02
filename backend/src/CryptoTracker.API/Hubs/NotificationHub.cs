using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CryptoTracker.API.Hubs;

/// <summary>
/// Tarayıcının açık tuttuğu bildirim bağlantısı. İstemci buraya mesaj göndermez, yalnızca dinler;
/// mesajlar IRealtimeNotifier üzerinden sunucu tarafından itilir.
/// Kullanıcı kimliği token'daki NameIdentifier claim'inden gelir, Clients.User(...) bunu kullanır.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public const string Path = "/hubs/notifications";
}
