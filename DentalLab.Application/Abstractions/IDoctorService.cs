using DentalLab.Domain.Entities;

namespace DentalLab.Application.Abstractions;

public interface IDoctorService
{
    Task<List<Doctor>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<Doctor?> GetByIdAsync(int docId, CancellationToken ct = default);
    Task<int> CreateAsync(Doctor doctor, CancellationToken ct = default);
    Task UpdateAsync(Doctor doctor, CancellationToken ct = default);
    Task SetActiveAsync(int docId, bool isActive, CancellationToken ct = default);
}