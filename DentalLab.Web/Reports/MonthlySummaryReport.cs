using System.Globalization;
using DentalLab.Application.Models.Cases;

namespace DentalLab.Web.Reports;

/// <summary>
/// All figures for one month's production summary. Built once from the case
/// list and used by both the Monthly Summary page and its PDF download.
/// </summary>
public sealed class MonthlySummaryReport
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string PrevLabel { get; init; } = "";
    public int Cases { get; init; }
    public int Units { get; init; }
    public decimal Amount { get; init; }
    public int Doctors { get; init; }
    public int PatientsCount { get; init; }
    public int PrevCases { get; init; }
    public int PrevUnits { get; init; }
    public decimal PrevAmount { get; init; }
    public int PrevDoctors { get; init; }
    public int Cancelled { get; init; }
    public int ToothUnits { get; init; }
    public int ArchUnits { get; init; }
    public decimal ToothAmount { get; init; }
    public decimal ArchAmount { get; init; }
    public List<TypeRow> Types { get; init; } = [];
    public List<DoctorRow> DoctorRows { get; init; } = [];
    public List<DayRow> Days { get; init; } = [];
    public List<WeekRow> Weeks { get; init; } = [];
    public int MaxDayUnits { get; init; }
    public int ActiveDays { get; init; }
    public int DaysInMonth { get; init; }
    public string BusiestDay { get; init; } = "-";
    public string TopDoctor { get; init; } = "-";
    public string TopType { get; init; } = "-";
    public string NewDoctors { get; init; } = "-";
    public List<UserRow> EnteredBy { get; init; } = [];

    public decimal UnitsPerCase => Cases == 0 ? 0 : (decimal)Units / Cases;
    public decimal AmountPerCase => Cases == 0 ? 0 : Amount / Cases;
    public decimal AmountPerUnit => Units == 0 ? 0 : Amount / Units;

    public static MonthlySummaryReport Build(DateOnly from, DateOnly to, List<CaseListItem> cases, List<CaseListItem> prev, int cancelled)
    {
        var lines = cases.SelectMany(c => c.Lines.Select(l => (Case: c, Line: l))).ToList();

        var units = cases.Sum(c => c.TotalUnits);
        var amount = cases.Sum(c => c.TotalAmount);

        var types = lines
            .GroupBy(x => x.Line.CaseTypeCode)
            .Select(g => new TypeRow(
                g.Key,
                g.First().Line.CaseTypeName,
                g.First().Line.Scope,
                g.Select(x => x.Case.CaseId).Distinct().Count(),
                g.Sum(x => x.Line.Units),
                g.Sum(x => x.Line.Amount)))
            .OrderByDescending(t => t.Amount).ThenByDescending(t => t.Units)
            .ToList();

        var prevByDoctor = prev.GroupBy(c => c.DocId)
            .ToDictionary(g => g.Key, g => (Units: g.Sum(c => c.TotalUnits), Amount: g.Sum(c => c.TotalAmount)));

        var doctors = cases
            .GroupBy(c => c.DocId)
            .Select(g =>
            {
                var topType = g.SelectMany(c => c.Lines)
                    .GroupBy(l => l.CaseTypeCode)
                    .OrderByDescending(x => x.Sum(l => l.Units))
                    .Select(x => x.Key)
                    .FirstOrDefault() ?? "-";
                prevByDoctor.TryGetValue(g.Key, out var p);
                return new DoctorRow(
                    g.First().DoctorName,
                    g.Count(),
                    g.Sum(c => c.TotalUnits),
                    g.Sum(c => c.TotalAmount),
                    topType,
                    p.Units,
                    p.Amount,
                    g.Key);
            })
            .OrderByDescending(d => d.Amount).ThenByDescending(d => d.Units)
            .ToList();

        var days = new List<DayRow>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var onDay = cases.Where(c => c.CaseDate == d).ToList();
            days.Add(new DayRow(d, onDay.Count, onDay.Sum(c => c.TotalUnits), d.DayOfWeek == DayOfWeek.Sunday));
        }

        var weeks = days
            .GroupBy(d => (d.Date.Day - 1) / 7)
            .Select(g => new WeekRow($"{g.First().Date.Day}–{g.Last().Date.Day}", g.Sum(x => x.Cases), g.Sum(x => x.Units)))
            .ToList();

        var busiest = days.OrderByDescending(d => d.Units).ThenByDescending(d => d.Cases).FirstOrDefault();

        var entered = cases
            .GroupBy(c => string.IsNullOrWhiteSpace(c.CreatedBy) ? "Unknown" : c.CreatedBy)
            .Select(g => new UserRow(g.Key, g.Count(), g.Sum(c => c.TotalUnits)))
            .OrderByDescending(u => u.Cases)
            .ToList();

        var prevDoctorIds = prev.Select(c => c.DocId).ToHashSet();
        var newDoctorCount = cases.Select(c => c.DocId).Distinct().Count(id => !prevDoctorIds.Contains(id));

        var toothLines = lines.Where(x => x.Line.Scope != 'A').ToList();
        var archLines = lines.Where(x => x.Line.Scope == 'A').ToList();

        return new MonthlySummaryReport
        {
            From = from,
            To = to,
            PrevLabel = from.AddMonths(-1).ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            Cases = cases.Count,
            Units = units,
            Amount = amount,
            Doctors = doctors.Count,
            PatientsCount = cases.Select(c => (c.PatientName ?? string.Empty).Trim().ToLowerInvariant())
                                 .Where(n => n.Length > 0).Distinct().Count(),
            PrevCases = prev.Count,
            PrevUnits = prev.Sum(c => c.TotalUnits),
            PrevAmount = prev.Sum(c => c.TotalAmount),
            PrevDoctors = prev.Select(c => c.DocId).Distinct().Count(),
            Cancelled = cancelled,
            ToothUnits = toothLines.Sum(x => x.Line.Units),
            ArchUnits = archLines.Sum(x => x.Line.Units),
            ToothAmount = toothLines.Sum(x => x.Line.Amount),
            ArchAmount = archLines.Sum(x => x.Line.Amount),
            Types = types,
            DoctorRows = doctors,
            Days = days,
            Weeks = weeks,
            MaxDayUnits = days.Count == 0 ? 0 : days.Max(d => d.Units),
            ActiveDays = days.Count(d => d.Cases > 0),
            DaysInMonth = days.Count,
            BusiestDay = busiest is null || busiest.Units == 0 ? "-" : $"{busiest.Date:ddd, dd MMM} ({busiest.Units} units)",
            TopDoctor = doctors.FirstOrDefault()?.Name ?? "-",
            TopType = types.FirstOrDefault() is { } top ? $"{top.Code} – {top.Name}" : "-",
            NewDoctors = newDoctorCount == 0 ? "None" : $"{newDoctorCount} not active last month",
            EnteredBy = entered
        };
    }

}

public sealed record TypeRow(string Code, string Name, char Scope, int Cases, int Units, decimal Amount);
public sealed record DoctorRow(string Name, int Cases, int Units, decimal Amount, string TopType, int PrevUnits, decimal PrevAmount, int DocId = 0);
public sealed record DayRow(DateOnly Date, int Cases, int Units, bool IsSunday);
public sealed record WeekRow(string Label, int Cases, int Units);
public sealed record UserRow(string Name, int Cases, int Units);
