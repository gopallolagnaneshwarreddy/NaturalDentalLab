using DentalLab.Domain.Entities;
namespace DentalLab.Application.Abstractions;

public interface ICaseTypeService
{
    Task<List<CaseType>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<CaseType?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<short> GetNextDisplayOrderAsync(CancellationToken ct = default);
    Task<int> CreateAsync(CaseType item, CancellationToken ct = default);
    Task UpdateAsync(CaseType item, CancellationToken ct = default);
    Task SetActiveAsync(int id, bool active, CancellationToken ct = default);
}
