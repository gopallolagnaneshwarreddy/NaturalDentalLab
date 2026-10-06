namespace DentalLab.Application.Models;

public sealed class DoctorRateListItem
{
    public int DocId { get; init; }
    public string DocName { get; init; } = string.Empty;
    public int CaseTypeId { get; init; }
    public string CaseTypeCode { get; init; } = string.Empty;
    public string CaseTypeName { get; init; } = string.Empty;
    public char Scope { get; init; }
    public short DisplayOrder { get; init; }
    public decimal Cost { get; init; }
    public bool IsActive { get; init; }
}
