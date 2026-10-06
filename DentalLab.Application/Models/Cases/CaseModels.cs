using System.ComponentModel.DataAnnotations;

namespace DentalLab.Application.Models.Cases;

public sealed class CreateCaseRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Select a doctor.")] public int DocId { get; set; }
    [Required] public DateOnly CaseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [StringLength(100)] public string? PatientName { get; set; }
    [StringLength(50)] public string? PatientRef { get; set; }
    [StringLength(250)] public string? Notes { get; set; }
    public List<CaseLineInput> Lines { get; set; } = [];
}

public sealed class CaseSaveResult
{
    public bool Succeeded { get; init; }
    public int? CaseId { get; init; }
    public string? ErrorMessage { get; init; }
    public static CaseSaveResult Success(int id) => new() { Succeeded = true, CaseId = id };
    public static CaseSaveResult Failure(string error) => new() { ErrorMessage = error };
}

public sealed class CaseFilter
{
    public int? CaseId { get; set; }
    public int? DocId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? Patient { get; set; }
    public string Status { get; set; } = "Active";
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortColumn { get; set; } = "CaseDate";
    public bool SortDescending { get; set; } = true;
}

public sealed class CaseLineView
{
    public byte LineNumber { get; init; }
    public int CaseTypeId { get; init; }
    public string CaseTypeCode { get; init; } = string.Empty;
    public string CaseTypeName { get; init; } = string.Empty;
    public char Scope { get; init; }
    public char? Arch { get; init; }
    public string? UR { get; init; }
    public string? UL { get; init; }
    public string? LR { get; init; }
    public string? LL { get; init; }
    public decimal UnitRate { get; init; }
    public int Units { get; init; }
    public decimal Amount { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed class CaseListItem
{
    public int CaseId { get; init; }
    public DateOnly CaseDate { get; init; }
    public int DocId { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public string? PatientName { get; init; }
    public string? PatientRef { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancelReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? ModifiedAt { get; init; }
    public string? ModifiedBy { get; init; }
    public int TotalUnits { get; init; }
    public decimal TotalAmount { get; init; }
    public List<CaseLineView> Lines { get; init; } = [];
}

public sealed class CaseDetails
{
    public int CaseId { get; init; }
    public DateOnly CaseDate { get; init; }
    public int DocId { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public string? PatientName { get; init; }
    public string? PatientRef { get; init; }
    public string? Notes { get; init; }
    public bool IsCancelled { get; init; }
    public string? CancelReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? ModifiedAt { get; init; }
    public string? ModifiedBy { get; init; }
    public int TotalUnits => Lines.Sum(x => x.Units);
    public decimal TotalAmount => Lines.Sum(x => x.Amount);
    public List<CaseLineView> Lines { get; init; } = [];
}

public sealed class CancelCaseRequest
{
    [Required(ErrorMessage = "Cancellation reason is required.")]
    [StringLength(250)] public string Reason { get; set; } = string.Empty;
}
