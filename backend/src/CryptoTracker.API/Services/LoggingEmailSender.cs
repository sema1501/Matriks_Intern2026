namespace CryptoTracker.API.Services;

/// <summary>
/// SMTP ayarı olmadığında kullanılan geliştirme fallback'i. E-postayı göndermek yerine
/// içeriğini loglar; böylece Görev 40'ın "gerçek SMTP yoksa en azından güvenilir şekilde
/// loglanıyor/görülebiliyor" kabul kriteri karşılanır.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[E-POSTA SİMÜLASYONU] SMTP yapılandırılmadığı için e-posta gönderilmedi.\n" +
            "  Alıcı : {To}\n  Konu  : {Subject}\n  İçerik: {Body}",
            to, subject, body);
        return Task.CompletedTask;
    }
}
