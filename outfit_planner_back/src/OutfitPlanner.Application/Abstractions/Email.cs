namespace OutfitPlanner.Application.Abstractions;

public sealed record EmailMessage(string ToEmail, string Subject, string TextBody);

// Outbound transactional email (password reset links). Implementations report whether delivery
// is actually configured so the API can offer an honest fallback instead of a form whose
// emails never arrive.
public interface IEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

// The password reset email itself. Kept in Application so the wording and the link shape are
// unit-tested without an SMTP server.
public static class PasswordResetEmail
{
    public const string ResetPath = "/reset-password";

    public static string BuildResetUrl(string publicOrigin, string token)
    {
        return $"{publicOrigin.TrimEnd('/')}{ResetPath}?token={Uri.EscapeDataString(token)}";
    }

    public static EmailMessage Build(string toEmail, string resetUrl, TimeSpan lifetime)
    {
        var minutes = Math.Max(1, (int)Math.Round(lifetime.TotalMinutes));
        var body =
            "Hello,\n\n" +
            "Someone asked to reset the password of the Outfit Planner account registered to this email address. " +
            "Open the link below to choose a new password:\n\n" +
            $"{resetUrl}\n\n" +
            $"The link works once and expires in {minutes} minutes. If you did not ask for a reset, ignore this email; " +
            "your password stays unchanged.\n\n" +
            "Outfit Planner";
        return new EmailMessage(toEmail, "Reset your Outfit Planner password", body);
    }
}
