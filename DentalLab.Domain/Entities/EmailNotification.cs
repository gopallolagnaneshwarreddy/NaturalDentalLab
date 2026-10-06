namespace DentalLab.Domain.Entities;

public sealed class EmailNotification
{
    public long EmailNotificationId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? SentOn { get; set; }
}
