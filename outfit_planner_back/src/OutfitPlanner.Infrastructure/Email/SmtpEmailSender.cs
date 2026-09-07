using System.Net;
using System.Net.Mail;
using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Infrastructure.Email;

public sealed record SmtpEmailOptions(
    string Host,
    int Port,
    string? Username,
    string? Password,
    bool UseStartTls,
    string FromAddress,
    string? FromName);

// Plain SMTP (STARTTLS on 587 by default) through the framework client: works with Gmail app
// passwords, Resend/Postmark/SES SMTP endpoints, or a local relay, without extra packages.
public sealed class SmtpEmailSender : IEmailSender
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(20);
    private readonly SmtpEmailOptions _options;

    public SmtpEmailSender(SmtpEmailOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new ArgumentException("SMTP host is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            throw new ArgumentException("A sender address is required.", nameof(options));
        }

        _options = options;
    }

    public bool IsConfigured => true;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseStartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = (int)SendTimeout.TotalMilliseconds
        };
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password ?? string.Empty);
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, string.IsNullOrWhiteSpace(_options.FromName) ? "Outfit Planner" : _options.FromName),
            Subject = message.Subject,
            Body = message.TextBody,
            IsBodyHtml = false
        };
        mail.To.Add(new MailAddress(message.ToEmail));
        await client.SendMailAsync(mail, cancellationToken);
    }
}

// No delivery configured: the API reports the feature as unavailable instead of pretending.
public sealed class DisabledEmailSender : IEmailSender
{
    public bool IsConfigured => false;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Email delivery is not configured on this server.");
    }
}
