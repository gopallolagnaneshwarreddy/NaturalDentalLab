using DentalLab.Application.Abstractions;
using DentalLab.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
namespace DentalLab.Web.Components.Account;
public sealed class SmtpIdentityEmailSender(IApplicationEmailService email) : IEmailSender<ApplicationUser>
{
    public async Task SendConfirmationLinkAsync(ApplicationUser user, string address, string link) =>
        await email.SendAsync(address, "Confirm your Dental Lab email", $"<p>Please confirm your account:</p><p><a href=\"{link}\">Confirm email</a></p>", "EmailConfirmation", "User", user.Id);
    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string address, string link) =>
        await email.SendAsync(address, "Reset your Dental Lab password", $"<p>A password reset was requested.</p><p><a href=\"{link}\">Reset password</a></p>", "PasswordReset", "User", user.Id);
    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string address, string code) =>
        await email.SendAsync(address, "Dental Lab password reset code", $"<p>Your reset code is <strong>{code}</strong>.</p>", "PasswordResetCode", "User", user.Id);
}
