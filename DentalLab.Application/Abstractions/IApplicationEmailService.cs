namespace DentalLab.Application.Abstractions;

public interface IApplicationEmailService
{
    Task<bool> SendAsync(string recipientEmail, string subject, string htmlBody,
        string notificationType, string? relatedEntityType = null,
        string? relatedEntityId = null, CancellationToken cancellationToken = default);
}
