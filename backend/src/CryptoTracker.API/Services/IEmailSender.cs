namespace CryptoTracker.API.Services;

/// <summary>
/// Basit e-posta gönderme soyutlaması. Gerçek SMTP ayarı varsa <see cref="SmtpEmailSender"/>,
/// yoksa geliştirme ortamında loglayan <see cref="LoggingEmailSender"/> kullanılır (Görev 40).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}

/// <summary>
/// SMTP ayarları. Değerler <c>appsettings.Development.json</c> üzerinden verilmelidir;
/// gizli bilgiler (kullanıcı/şifre) asla repoya commit edilmez.
/// </summary>
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "no-reply@cryptotracker.local";
    public string FromName { get; set; } = "CryptoTracker";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
