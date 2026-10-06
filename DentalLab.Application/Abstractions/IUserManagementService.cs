using DentalLab.Application.Models;
namespace DentalLab.Application.Abstractions;
public interface IUserManagementService
{
    Task<List<UserListItem>> GetUsersAsync(string currentUserId, CancellationToken cancellationToken = default);
    Task<List<string>> GetAssignableRolesAsync(string currentUserId, CancellationToken cancellationToken = default);
    Task<UserOperationResult> CreateUserAsync(string currentUserId, CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserOperationResult> ResendInvitationAsync(string currentUserId, string targetUserId, CancellationToken cancellationToken = default);
    Task<UserOperationResult> SetUserLockoutAsync(string currentUserId, string targetUserId, bool lockUser, CancellationToken cancellationToken = default);
}
