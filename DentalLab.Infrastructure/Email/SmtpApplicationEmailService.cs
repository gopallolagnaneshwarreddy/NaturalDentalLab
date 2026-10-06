using DentalLab.Application.Abstractions;
using DentalLab.Domain.Entities;
using DentalLab.Infrastructure.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
namespace DentalLab.Infrastructure.Email;
public sealed class SmtpApplicationEmailService(IDbContextFactory<ApplicationDbContext> factory, IOptions<SmtpOptions> options) : IApplicationEmailService
{
    public async Task<bool> SendAsync(string recipientEmail, string subject, string htmlBody, string notificationType,
        string? relatedEntityType = null, string? relatedEntityId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var log = new EmailNotification { RecipientEmail = recipientEmail, Subject = subject, NotificationType = notificationType,
            RelatedEntityType = relatedEntityType, RelatedEntityId = relatedEntityId, Status = "Pending", CreatedOn = DateTime.UtcNow };
        db.EmailNotifications.Add(log); await db.SaveChangesAsync(cancellationToken);
        try
        {
            var o = options.Value;
            if (string.IsNullOrWhiteSpace(o.Host) || string.IsNullOrWhiteSpace(o.FromEmail)) throw new InvalidOperationException("SMTP configuration is incomplete.");
            var message = new MimeMessage(); message.From.Add(new MailboxAddress(o.FromName, o.FromEmail));
            message.To.Add(MailboxAddress.Parse(recipientEmail)); message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();
            using var client = new SmtpClient();

            client.CheckCertificateRevocation = false;

            client.ServerCertificateValidationCallback = (_, _, _, _) => true;

            var security = o.Security.ToLowerInvariant() switch { "ssl" or "sslonconnect" => SecureSocketOptions.SslOnConnect, "none" => SecureSocketOptions.None, _ => SecureSocketOptions.StartTls };
            await client.ConnectAsync(o.Host, o.Port, security, cancellationToken);
            if (!string.IsNullOrWhiteSpace(o.UserName)) await client.AuthenticateAsync(o.UserName, o.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken); await client.DisconnectAsync(true, cancellationToken);
            log.Status = "Sent"; log.AttemptCount = 1; log.SentOn = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken); return true;
        }
        catch (Exception ex)
        {
            log.Status = "Failed"; log.AttemptCount = 1; log.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(cancellationToken); return false;
        }
    }
}
