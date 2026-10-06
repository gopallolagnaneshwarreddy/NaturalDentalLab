using DentalLab.Application.Models.Approvals;

namespace DentalLab.Application.Abstractions;

public interface ICaseApprovalService
{
    Task<List<AdminOption>> GetActiveAdminsAsync(CancellationToken ct = default);
    Task<ApprovalOperationResult> CreateAsync(CreateCaseApprovalRequest request, string requestedByUserId, string requestedByEmail, CancellationToken ct = default);
    Task<List<CaseApprovalListItem>> GetForCurrentUserAsync(string currentUserId, bool isOwner, CancellationToken ct = default);
    Task<CaseApprovalDetails?> GetByIdAsync(long requestId, string currentUserId, bool isOwner, CancellationToken ct = default);
    Task<ApprovalOperationResult> ApproveAsync(long requestId, string comment, byte[] rowVersion, string reviewerUserId, string reviewerEmail, bool isOwner, CancellationToken ct = default);
    Task<ApprovalOperationResult> RejectAsync(long requestId, string comment, byte[] rowVersion, string reviewerUserId, string reviewerEmail, bool isOwner, CancellationToken ct = default);
    Task<ApprovalOperationResult> CompleteEditAsync(long requestId, int caseId, string completedByUserId, string completedByEmail, bool isOwner, CancellationToken ct = default);
    Task<ApprovalOperationResult> ApproveAndCancelAsync(long requestId, string comment, byte[] rowVersion, string reviewerUserId, string reviewerEmail, bool isOwner, CancellationToken ct = default);
    Task<ApprovalOperationResult> ResumeCancellationAsync(long requestId, string reviewerUserId, string reviewerEmail, bool isOwner, CancellationToken ct = default);
}
