using System.Security.Cryptography;
using DentalLab.Application.Abstractions;
using DentalLab.Application.Models;
using DentalLab.Application.Security;
using DentalLab.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace DentalLab.Infrastructure.Identity;
public sealed class UserManagementService(UserManager<ApplicationUser> userManager, IApplicationEmailService email) : IUserManagementService
{
    public async Task<List<UserListItem>> GetUsersAsync(string currentUserId, CancellationToken ct = default)
    {
        var current = await FindCurrentUserAsync(currentUserId); var role = await GetHighestRoleAsync(current);
        if (role is null) return [];
        var users = await userManager.Users.AsNoTracking().OrderBy(x => x.Email).ToListAsync(ct); var result = new List<UserListItem>();
        foreach (var u in users)
        {
            var userRole = await GetHighestRoleAsync(u); if (!CanViewRole(role, userRole)) continue;
            result.Add(new UserListItem { UserId=u.Id, Email=u.Email??"", Role=userRole??"Unassigned", EmailConfirmed=u.EmailConfirmed,
                IsLocked=IsUserLocked(u), MustChangePassword=u.MustChangePassword, InvitationEmailStatus=u.InvitationEmailStatus,
                InvitationEmailSentOn=u.InvitationEmailSentOn, InvitationEmailError=u.InvitationEmailError });
        }
        return result;
    }
    public async Task<List<string>> GetAssignableRolesAsync(string currentUserId, CancellationToken ct = default)
    {
        var role=await GetHighestRoleAsync(await FindCurrentUserAsync(currentUserId));
        return role switch { ApplicationRoles.Owner => [ApplicationRoles.Admin,ApplicationRoles.Staff], ApplicationRoles.Admin => [ApplicationRoles.Staff], _ => [] };
    }
    public async Task<UserOperationResult> CreateUserAsync(string currentUserId, CreateUserRequest request, CancellationToken ct = default)
    {
        var currentRole=await GetHighestRoleAsync(await FindCurrentUserAsync(currentUserId)); request.Email=request.Email.Trim(); request.Role=request.Role.Trim();
        if(!CanCreateRole(currentRole,request.Role)) return UserOperationResult.Failure("You are not allowed to create this role.");
        if(await userManager.FindByEmailAsync(request.Email) is not null) return UserOperationResult.Failure("A user with this email already exists.");
        var password=GenerateTemporaryPassword();
        var user=new ApplicationUser {UserName=request.Email,Email=request.Email,EmailConfirmed=true,LockoutEnabled=true,MustChangePassword=true,
            TemporaryPasswordIssuedOn=DateTime.UtcNow,InvitationEmailStatus="Pending"};
        var created=await userManager.CreateAsync(user,password); if(!created.Succeeded) return UserOperationResult.Failure(Errors(created));
        var assigned=await userManager.AddToRoleAsync(user,request.Role); if(!assigned.Succeeded){await userManager.DeleteAsync(user);return UserOperationResult.Failure(Errors(assigned));}
        await SendInvitationAsync(user,password,ct);
        return UserOperationResult.Success();
    }
    public async Task<UserOperationResult> ResendInvitationAsync(string currentUserId,string targetUserId,CancellationToken ct=default)
    {
        var currentRole=await GetHighestRoleAsync(await FindCurrentUserAsync(currentUserId)); var user=await userManager.FindByIdAsync(targetUserId);
        if(user is null) return UserOperationResult.Failure("User not found."); var targetRole=await GetHighestRoleAsync(user);
        if(!CanManageRole(currentRole,targetRole)) return UserOperationResult.Failure("You cannot manage this user.");
        var password=GenerateTemporaryPassword(); var token=await userManager.GeneratePasswordResetTokenAsync(user); var reset=await userManager.ResetPasswordAsync(user,token,password);
        if(!reset.Succeeded) return UserOperationResult.Failure(Errors(reset));
        user.MustChangePassword=true; user.TemporaryPasswordIssuedOn=DateTime.UtcNow; user.PasswordChangedOn=null; user.InvitationEmailStatus="Pending"; user.InvitationEmailError=null;
        await userManager.UpdateAsync(user); await SendInvitationAsync(user,password,ct); return UserOperationResult.Success();
    }
    public async Task<UserOperationResult> SetUserLockoutAsync(string currentUserId,string targetUserId,bool lockUser,CancellationToken ct=default)
    {
        if(currentUserId==targetUserId)return UserOperationResult.Failure("You cannot disable your own account.");
        var currentRole=await GetHighestRoleAsync(await FindCurrentUserAsync(currentUserId));var user=await userManager.FindByIdAsync(targetUserId);
        if(user is null)return UserOperationResult.Failure("User not found.");if(!CanManageRole(currentRole,await GetHighestRoleAsync(user)))return UserOperationResult.Failure("You cannot manage this user.");
        await userManager.SetLockoutEnabledAsync(user,true);var result=await userManager.SetLockoutEndDateAsync(user,lockUser?DateTimeOffset.MaxValue:null);
        return result.Succeeded?UserOperationResult.Success():UserOperationResult.Failure(Errors(result));
    }
    private async Task SendInvitationAsync(ApplicationUser user,string password,CancellationToken ct)
    {
        var body=$"<h2>Dental Lab account</h2><p>Your account has been created.</p><p>Email: <strong>{user.Email}</strong></p><p>Temporary password: <strong>{System.Net.WebUtility.HtmlEncode(password)}</strong></p><p>Sign in and change this password immediately.</p>";
        var sent=await email.SendAsync(user.Email!,"Your Dental Lab account",body,"UserInvitation","User",user.Id,ct);
        user.InvitationEmailStatus=sent?"Sent":"Failed";user.InvitationEmailSentOn=sent?DateTime.UtcNow:null;user.InvitationEmailError=sent?null:"SMTP delivery failed. See EmailNotifications.";await userManager.UpdateAsync(user);
    }
    private static string GenerateTemporaryPassword(){const string chars="ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$?";Span<byte>b=stackalloc byte[16];RandomNumberGenerator.Fill(b);return new string(b.ToArray().Select(x=>chars[x%chars.Length]).ToArray())+"aA1!";}
    private async Task<ApplicationUser> FindCurrentUserAsync(string id)=>await userManager.FindByIdAsync(id)??throw new UnauthorizedAccessException();
    private async Task<string?> GetHighestRoleAsync(ApplicationUser u){var rs=await userManager.GetRolesAsync(u);return rs.Contains(ApplicationRoles.Owner)?ApplicationRoles.Owner:rs.Contains(ApplicationRoles.Admin)?ApplicationRoles.Admin:rs.Contains(ApplicationRoles.Staff)?ApplicationRoles.Staff:null;}
    private static bool CanCreateRole(string? c,string t)=>c==ApplicationRoles.Owner&&t is ApplicationRoles.Admin or ApplicationRoles.Staff||c==ApplicationRoles.Admin&&t==ApplicationRoles.Staff;
    private static bool CanViewRole(string c,string? t)=>CanCreateRole(c,t??"");private static bool CanManageRole(string? c,string? t)=>CanCreateRole(c,t??"");
    private static bool IsUserLocked(ApplicationUser u)=>u.LockoutEnabled&&u.LockoutEnd>DateTimeOffset.UtcNow;private static string Errors(IdentityResult r)=>string.Join("; ",r.Errors.Select(x=>x.Description));
}
