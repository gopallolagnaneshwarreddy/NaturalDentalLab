using Microsoft.AspNetCore.Identity;
namespace DentalLab.Infrastructure.Data;
public class ApplicationUser : IdentityUser
{
    public bool MustChangePassword { get; set; }
    public DateTime? TemporaryPasswordIssuedOn { get; set; }
    public DateTime? PasswordChangedOn { get; set; }
    public string InvitationEmailStatus { get; set; } = "NotSent";
    public DateTime? InvitationEmailSentOn { get; set; }
    public string? InvitationEmailError { get; set; }
}
