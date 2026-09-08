using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace CryptoTracker.API.Services;

/// <summary>
/// Gerçek SMTP sunucusu üzerinden e-posta gönderir. Yalnızca <see cref="SmtpOptions.Host"/>
/// yapılandırıldığında register edilir (bkz. Program.cs).
/// </summary>
public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(to);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(_options.User)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.User, _options.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
        logger.LogInformation("E-posta gönderildi: To={To}, Subject={Subject}", to, subject);
    }
}
