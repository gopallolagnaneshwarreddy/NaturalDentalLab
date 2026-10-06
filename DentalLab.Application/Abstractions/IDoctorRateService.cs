using DentalLab.Application.Models;
using DentalLab.Domain.Entities;
namespace DentalLab.Application.Abstractions;

public interface IDoctorRateService
{
    Task<List<DoctorRateListItem>> GetAllAsync(int? docId = null, int? caseTypeId = null, bool includeInactive = false, CancellationToken ct = default);
    Task<List<DoctorRateListItem>> GetForDoctorAsync(int docId, bool includeInactive = false, CancellationToken ct = default);
    Task<List<CaseType>> GetAvailableCaseTypesAsync(int docId, CancellationToken ct = default);
    Task<DoctorRate?> GetAsync(int docId, int caseTypeId, CancellationToken ct = default);
    Task CreateAsync(DoctorRate rate, CancellationToken ct = default);
    Task UpdateAsync(DoctorRate rate, CancellationToken ct = default);
    Task SetActiveAsync(int docId, int caseTypeId, bool active, CancellationToken ct = default);
}
