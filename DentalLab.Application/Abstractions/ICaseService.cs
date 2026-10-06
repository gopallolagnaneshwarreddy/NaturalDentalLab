using DentalLab.Application.Models.Cases;
using DentalLab.Application.Models.Common;

namespace DentalLab.Application.Abstractions;

public interface ICaseService
{
    Task<CaseSaveResult> CreateAsync(CreateCaseRequest request, string createdBy, CancellationToken ct = default);
    Task<PagedResult<CaseListItem>> SearchAsync(CaseFilter filter, CancellationToken ct = default);
    Task<CaseDetails?> GetByIdAsync(int caseId, CancellationToken ct = default);
    Task<CaseSaveResult> UpdateAsync(int caseId, CreateCaseRequest request, string modifiedBy, CancellationToken ct = default);
    Task<CaseSaveResult> CancelAsync(int caseId, string reason, string modifiedBy, CancellationToken ct = default);
}
