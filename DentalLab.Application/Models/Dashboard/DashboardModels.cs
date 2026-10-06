namespace DentalLab.Application.Models.Dashboard;

public sealed class DashboardFilter
{
    public int Month { get; set; } = DateTime.Today.Month;
    public int Year { get; set; } = DateTime.Today.Year;
    public int? DoctorId { get; set; }
}

public sealed class DashboardDoctorOption
{
    public int DoctorId { get; init; }
    public string DoctorName { get; init; } = string.Empty;
}

public sealed class DashboardSummary
{
    public int CasesToday { get; init; }
    public int UnitsToday { get; init; }
    public decimal AmountToday { get; init; }
    public int CasesThisMonth { get; init; }
    public int UnitsThisMonth { get; init; }
    public decimal AmountThisMonth { get; init; }
    public int ActiveDoctors { get; init; }
    public int PendingRequests { get; init; }
    public int ApprovedAwaitingAction { get; init; }
    public int CompletedRequests { get; init; }
    public int RejectedRequests { get; init; }
    public int FailedEmails { get; init; }
}

public sealed class DoctorProductionSummary
{
    public int DoctorId { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public int CaseCount { get; init; }
    public int Units { get; init; }
    public decimal Amount { get; init; }
}

public sealed class CaseTypeProductionSummary
{
    public int CaseTypeId { get; init; }
    public string CaseTypeCode { get; init; } = string.Empty;
    public int CaseCount { get; init; }
    public int Units { get; init; }
    public decimal Amount { get; init; }
}

public sealed class DailyProductionSummary
{
    public DateOnly Date { get; init; }
    public int CaseCount { get; init; }
    public int Units { get; init; }
    public decimal Amount { get; init; }
}

public sealed class RecentCaseSummary
{
    public int CaseId { get; init; }
    public DateOnly CaseDate { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public string PatientName { get; init; } = string.Empty;
    public string TypeCodes { get; init; } = string.Empty;
    public int Units { get; init; }
    public decimal Amount { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public bool IsCancelled { get; init; }
}

public sealed class DashboardApprovalSummary
{
    public long RequestId { get; init; }
    public int CaseId { get; init; }
    public string RequestType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string RequestedByEmail { get; init; } = string.Empty;
    public string AssignedAdminEmail { get; init; } = string.Empty;
    public DateTime RequestedOn { get; init; }
}

public sealed class DashboardData
{
    public DashboardSummary Summary { get; init; } = new();
    public List<DashboardDoctorOption> Doctors { get; init; } = [];
    public List<DoctorProductionSummary> DoctorProduction { get; init; } = [];
    public List<CaseTypeProductionSummary> CaseTypeProduction { get; init; } = [];
    public List<DailyProductionSummary> DailyProduction { get; init; } = [];
    public List<RecentCaseSummary> RecentCases { get; init; } = [];
    public List<DashboardApprovalSummary> ApprovalRequests { get; init; } = [];
}
