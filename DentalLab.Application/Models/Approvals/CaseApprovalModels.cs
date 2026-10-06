using System.ComponentModel.DataAnnotations;

namespace DentalLab.Application.Models.Approvals;

public sealed class AdminOption
{
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class CreateCaseApprovalRequest
{
    [Range(1, int.MaxValue)]
    public int CaseId { get; set; }

    [Required]
    public string RequestType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select an admin.")]
    public string AssignedAdminUserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Reason is required.")]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? AdditionalDetails { get; set; }
}

public sealed class ReviewCaseApprovalRequest
{
    [Required(ErrorMessage = "Review comment is required.")]
    [StringLength(500)]
    public string Comment { get; set; } = string.Empty;
}

public sealed class CaseApprovalListItem
{
    public long RequestId { get; init; }
    public int CaseId { get; init; }
    public string RequestType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string RequestedByEmail { get; init; } = string.Empty;
    public string AssignedAdminEmail { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime RequestedOn { get; init; }
    public DateTime? ReviewedOn { get; init; }
}

public sealed class CaseApprovalDetails
{
    public long RequestId { get; init; }
    public int CaseId { get; init; }
    public string RequestType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string RequestedByUserId { get; init; } = string.Empty;
    public string RequestedByEmail { get; init; } = string.Empty;
    public string AssignedAdminUserId { get; init; } = string.Empty;
    public string AssignedAdminEmail { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? AdditionalDetails { get; init; }
    public DateTime RequestedOn { get; init; }
    public string? ReviewedByEmail { get; init; }
    public DateTime? ReviewedOn { get; init; }
    public string? ReviewComment { get; init; }
    public DateTime? CompletedOn { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed class ApprovalOperationResult
{
    public bool Succeeded { get; init; }
    public long? RequestId { get; init; }
    public string? ErrorMessage { get; init; }

    public static ApprovalOperationResult Success(long requestId) =>
        new() { Succeeded = true, RequestId = requestId };

    public static ApprovalOperationResult Failure(string error) =>
        new() { ErrorMessage = error };
}
