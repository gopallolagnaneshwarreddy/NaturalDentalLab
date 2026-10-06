namespace DentalLab.Domain.Entities;

public sealed class CaseApprovalRequest
{
    public long RequestId { get; set; }
    public int CaseId { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByEmail { get; set; } = string.Empty;
    public string AssignedAdminUserId { get; set; } = string.Empty;
    public string AssignedAdminEmail { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? AdditionalDetails { get; set; }
    public DateTime RequestedOn { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByEmail { get; set; }
    public DateTime? ReviewedOn { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? CompletedOn { get; set; }
    public string? CompletedByUserId { get; set; }
    public string? CompletedByEmail { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
