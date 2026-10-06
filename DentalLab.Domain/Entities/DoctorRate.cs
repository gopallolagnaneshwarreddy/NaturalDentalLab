namespace DentalLab.Domain.Entities;

public class DoctorRate
{
    public int DocId { get; set; }
    public int CaseTypeId { get; set; }
    public decimal Cost { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public CaseType CaseType { get; set; } = null!;
}
