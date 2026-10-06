namespace DentalLab.Domain.Entities;

public class Doctor
{
    public int DocId { get; set; }
    public string DocName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public ICollection<DoctorRate> Rates { get; set; } = new List<DoctorRate>();
    public ICollection<Record> Records { get; set; } = new List<Record>();
}
