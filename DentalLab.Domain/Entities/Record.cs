namespace DentalLab.Domain.Entities;

public class Record
{
    public int CaseId { get; set; }
    public byte LineNumber { get; set; } = 1;
    public DateOnly CaseDate { get; set; }
    public int DocId { get; set; }
    public string? PatientName { get; set; }
    public string? PatientRef { get; set; }
    public int CaseTypeId { get; set; }
    public char Scope { get; set; } = 'T';
    public char? Arch { get; set; }
    public string? UR { get; set; }
    public string? UL { get; set; }
    public string? LR { get; set; }
    public string? LL { get; set; }
    public decimal UnitRate { get; set; }
    public int Units { get; private set; }
    public decimal Amount { get; private set; }
    public string? Notes { get; set; }
    public bool IsCancelled { get; set; }
    public string? CancelReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Doctor Doctor { get; set; } = null!;
    public CaseType CaseType { get; set; } = null!;
    public DoctorRate DoctorRate { get; set; } = null!;
}
