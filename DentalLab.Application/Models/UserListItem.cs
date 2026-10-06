namespace DentalLab.Application.Models;
public sealed class UserListItem
{
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool EmailConfirmed { get; init; }
    public bool IsLocked { get; init; }
    public bool MustChangePassword { get; init; }
    public string InvitationEmailStatus { get; init; } = "NotSent";
    public DateTime? InvitationEmailSentOn { get; init; }
    public string? InvitationEmailError { get; init; }
}
