namespace DentalLab.Domain.Entities;

public class CaseType
{
    public int CaseTypeId { get; set; }
    public string CaseTypeCode { get; set; } = string.Empty;
    public string CaseTypeName { get; set; } = string.Empty;
    public char Scope { get; set; } = 'T';
    public short DisplayOrder { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public ICollection<DoctorRate> DoctorRates { get; set; } = new List<DoctorRate>();
    public ICollection<Record> Records { get; set; } = new List<Record>();
}
