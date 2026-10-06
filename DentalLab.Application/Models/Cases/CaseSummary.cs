namespace DentalLab.Application.Models.Cases;

public sealed class CaseSummary
{
    public int CaseId { get; init; }
    public DateOnly CaseDate { get; init; }
    public int DocId { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public string? PatientName { get; init; }
    public string? PatientRef { get; init; }
    public string CaseTypeText { get; init; } = string.Empty;
    public int TotalUnits { get; init; }
    public decimal TotalAmount { get; init; }
    public int LineCount { get; init; }
    public bool IsCancelled { get; init; }
}
