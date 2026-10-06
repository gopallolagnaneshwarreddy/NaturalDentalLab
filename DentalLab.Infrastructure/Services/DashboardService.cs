using DentalLab.Application.Abstractions;
using DentalLab.Application.Models.Dashboard;
using DentalLab.Application.Security;
using DentalLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Infrastructure.Services;

public sealed class DashboardService(
    IDbContextFactory<ApplicationDbContext> factory) : IDashboardService
{
    public async Task<DashboardData> GetDashboardAsync(
        DashboardFilter filter,
        string currentUserId,
        string currentUserEmail,
        string role,
        CancellationToken cancellationToken = default)
    {
        ValidateFilter(filter);

        await using var db =
            await factory.CreateDbContextAsync(cancellationToken);

        var monthStart = new DateOnly(filter.Year, filter.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var isOwner = role == ApplicationRoles.Owner;
        var isAdmin = role == ApplicationRoles.Admin;
        var isStaff = role == ApplicationRoles.Staff;

        var doctors = await db.Doctors
            .AsNoTracking()
            .Where(doctor => doctor.IsActive)
            .OrderBy(doctor => doctor.DocName)
            .Select(doctor => new DashboardDoctorOption
            {
                DoctorId = doctor.DocId,
                DoctorName = doctor.DocName
            })
            .ToListAsync(cancellationToken);

        var records = db.Records
            .AsNoTracking()
            .Where(record =>
                record.CaseDate >= monthStart &&
                record.CaseDate < monthEnd);

        if (filter.DoctorId.HasValue)
        {
            records = records.Where(record =>
                record.DocId == filter.DoctorId.Value);
        }

        if (isStaff)
        {
            records = records.Where(record =>
                record.CreatedBy == currentUserEmail);
        }

        var monthRows = await records
            .Select(record => new DashboardRecordRow
            {
                CaseId = record.CaseId,
                CaseDate = record.CaseDate,
                DoctorId = record.DocId,
                DoctorName = record.Doctor.DocName,
                PatientName = record.PatientName!,
                CaseTypeId = record.CaseTypeId,
                CaseTypeCode = record.CaseType.CaseTypeCode,
                Units = record.Units,
                Amount = record.Amount,
                CreatedBy = record.CreatedBy,
                IsCancelled = record.IsCancelled
            })
            .ToListAsync(cancellationToken);

        var activeMonthRows = monthRows
            .Where(row => !row.IsCancelled)
            .ToList();

        var todayRows = activeMonthRows
            .Where(row => row.CaseDate == today)
            .ToList();

        var approvals = db.CaseApprovalRequests.AsNoTracking();

        if (isAdmin)
        {
            approvals = approvals.Where(request =>
                request.AssignedAdminUserId == currentUserId);
        }
        else if (isStaff)
        {
            approvals = approvals.Where(request =>
                request.RequestedByUserId == currentUserId);
        }

        var approvalRows = await approvals
            .OrderByDescending(request => request.RequestedOn)
            .Take(10)
            .Select(request => new DashboardApprovalSummary
            {
                RequestId = request.RequestId,
                CaseId = request.CaseId,
                RequestType = request.RequestType,
                Status = request.Status,
                RequestedByEmail = request.RequestedByEmail,
                AssignedAdminEmail = request.AssignedAdminEmail,
                RequestedOn = request.RequestedOn
            })
            .ToListAsync(cancellationToken);

        var pendingRequests = await approvals.CountAsync(
            request =>
                request.Status ==
                CaseApprovalRequestStatuses.Pending,
            cancellationToken);

        var approvedRequests = await approvals.CountAsync(
            request =>
                request.Status ==
                CaseApprovalRequestStatuses.Approved,
            cancellationToken);

        var completedRequests = await approvals.CountAsync(
            request =>
                request.Status ==
                CaseApprovalRequestStatuses.Completed,
            cancellationToken);

        var rejectedRequests = await approvals.CountAsync(
            request =>
                request.Status ==
                CaseApprovalRequestStatuses.Rejected,
            cancellationToken);

        var failedEmails = isOwner
            ? await db.EmailNotifications
                .AsNoTracking()
                .CountAsync(
                    email => email.Status == "Failed",
                    cancellationToken)
            : 0;

        var doctorProduction = activeMonthRows
            .GroupBy(row => new
            {
                row.DoctorId,
                row.DoctorName
            })
            .Select(group => new DoctorProductionSummary
            {
                DoctorId = group.Key.DoctorId,
                DoctorName = group.Key.DoctorName,
                CaseCount = group
                    .Select(row => row.CaseId)
                    .Distinct()
                    .Count(),
                Units = group.Sum(row => row.Units),
                Amount = group.Sum(row => row.Amount)
            })
            .OrderByDescending(row => row.Amount)
            .ThenBy(row => row.DoctorName)
            .ToList();

        var caseTypeProduction = activeMonthRows
            .GroupBy(row => new
            {
                row.CaseTypeId,
                row.CaseTypeCode
            })
            .Select(group => new CaseTypeProductionSummary
            {
                CaseTypeId = group.Key.CaseTypeId,
                CaseTypeCode = group.Key.CaseTypeCode,
                CaseCount = group
                    .Select(row => row.CaseId)
                    .Distinct()
                    .Count(),
                Units = group.Sum(row => row.Units),
                Amount = group.Sum(row => row.Amount)
            })
            .OrderByDescending(row => row.Units)
            .ThenBy(row => row.CaseTypeCode)
            .ToList();

        var dailyProduction = activeMonthRows
            .GroupBy(row => row.CaseDate)
            .Select(group => new DailyProductionSummary
            {
                Date = group.Key,
                CaseCount = group
                    .Select(row => row.CaseId)
                    .Distinct()
                    .Count(),
                Units = group.Sum(row => row.Units),
                Amount = group.Sum(row => row.Amount)
            })
            .OrderBy(row => row.Date)
            .ToList();

        var recentCases = monthRows
            .GroupBy(row => new
            {
                row.CaseId,
                row.CaseDate,
                row.DoctorName,
                row.PatientName,
                row.CreatedBy,
                row.IsCancelled
            })
            .Select(group => new RecentCaseSummary
            {
                CaseId = group.Key.CaseId,
                CaseDate = group.Key.CaseDate,
                DoctorName = group.Key.DoctorName,
                PatientName = group.Key.PatientName,
                TypeCodes = string.Join(
                    ", ",
                    group.Select(row => row.CaseTypeCode)
                        .Distinct()
                        .OrderBy(code => code)),
                Units = group
                    .Where(row => !row.IsCancelled)
                    .Sum(row => row.Units),
                Amount = group
                    .Where(row => !row.IsCancelled)
                    .Sum(row => row.Amount),
                CreatedBy = group.Key.CreatedBy,
                IsCancelled = group.Key.IsCancelled
            })
            .OrderByDescending(row => row.CaseDate)
            .ThenByDescending(row => row.CaseId)
            .Take(10)
            .ToList();

        var summary = new DashboardSummary
        {
            CasesToday = todayRows
                .Select(row => row.CaseId)
                .Distinct()
                .Count(),
            UnitsToday = todayRows.Sum(row => row.Units),
            AmountToday = todayRows.Sum(row => row.Amount),
            CasesThisMonth = activeMonthRows
                .Select(row => row.CaseId)
                .Distinct()
                .Count(),
            UnitsThisMonth = activeMonthRows.Sum(row => row.Units),
            AmountThisMonth = activeMonthRows.Sum(row => row.Amount),
            ActiveDoctors = doctors.Count,
            PendingRequests = pendingRequests,
            ApprovedAwaitingAction = approvedRequests,
            CompletedRequests = completedRequests,
            RejectedRequests = rejectedRequests,
            FailedEmails = failedEmails
        };

        return new DashboardData
        {
            Summary = summary,
            Doctors = doctors,
            DoctorProduction = doctorProduction,
            CaseTypeProduction = caseTypeProduction,
            DailyProduction = dailyProduction,
            RecentCases = recentCases,
            ApprovalRequests = approvalRows
        };
    }

    private static void ValidateFilter(DashboardFilter filter)
    {
        if (filter.Month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter.Month),
                "Month must be between 1 and 12.");
        }

        if (filter.Year is < 2000 or > 2100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter.Year),
                "Year must be between 2000 and 2100.");
        }
    }

    private sealed class DashboardRecordRow
    {
        public int CaseId { get; init; }
        public DateOnly CaseDate { get; init; }
        public int DoctorId { get; init; }
        public string DoctorName { get; init; } = string.Empty;
        public string PatientName { get; init; } = string.Empty;
        public int CaseTypeId { get; init; }
        public string CaseTypeCode { get; init; } = string.Empty;
        public int Units { get; init; }
        public decimal Amount { get; init; }
        public string CreatedBy { get; init; } = string.Empty;
        public bool IsCancelled { get; init; }
    }
}
