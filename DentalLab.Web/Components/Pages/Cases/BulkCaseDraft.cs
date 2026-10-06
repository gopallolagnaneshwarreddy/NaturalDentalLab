using System.ComponentModel.DataAnnotations;
using DentalLab.Application.Models.Cases;

namespace DentalLab.Web.Components.Pages.Cases;

/// <summary>
/// One case waiting in an owner bulk-entry batch (not yet saved).
/// </summary>
public sealed class BulkCaseDraft
{
    public Guid DraftId { get; } = Guid.NewGuid();

    public int DocId { get; set; }

    public string DocName { get; set; } = string.Empty;

    public DateOnly? CaseDate { get; set; }

    public string? PatientName { get; set; }

    public string? PatientRef { get; set; }

    public string? Notes { get; set; }

    public List<CaseLineInput> Lines { get; set; } = [];

    /// <summary>Error from the last save attempt, if this case failed.</summary>
    public string? SaveError { get; set; }

    public int TotalUnits => Lines.Sum(line => line.Units);

    public decimal TotalAmount => Lines.Sum(line => line.Amount);

    public string LinesSummary => string.Join(", ", Lines.Select(line =>
        string.IsNullOrWhiteSpace(line.SelectionText)
            ? line.CaseTypeCode
            : $"{line.CaseTypeCode} ({line.SelectionText})"));

    public CreateCaseRequest ToRequest(DateOnly caseDate)
    {
        // Optional fields are only set when filled in, so the request keeps
        // the same defaults as the single New Case page.
        var request = new CreateCaseRequest
        {
            DocId = DocId,
            CaseDate = caseDate,
            PatientName = PatientName?.Trim() ?? string.Empty,
            Lines = Lines
        };

        if (!string.IsNullOrWhiteSpace(PatientRef))
        {
            request.PatientRef = PatientRef.Trim();
        }

        if (!string.IsNullOrWhiteSpace(Notes))
        {
            request.Notes = Notes.Trim();
        }

        return request;
    }

    /// <summary>
    /// Runs the same DataAnnotations rules the single New Case page uses,
    /// so a case can't be queued if it would fail validation on save.
    /// </summary>
    public static List<string> Validate(CreateCaseRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        var errors = results
            .Select(result => result.ErrorMessage ?? "Invalid value.")
            .ToList();

        if (request.Lines.Count == 0)
        {
            errors.Add("Add at least one case line.");
        }

        if (request.CaseDate > DateOnly.FromDateTime(DateTime.Today))
        {
            errors.Add("Case date cannot be in the future.");
        }

        return errors.Distinct().ToList();
    }
}
