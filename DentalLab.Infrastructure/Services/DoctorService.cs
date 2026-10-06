using DentalLab.Application.Abstractions;
using DentalLab.Domain.Entities;
using DentalLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Infrastructure.Services;

public sealed class DoctorService(
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : IDoctorService
{
    public async Task<List<Doctor>> GetAllAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Doctors
            .Where(doctor => includeInactive || doctor.IsActive)
            .OrderBy(doctor => doctor.DocName)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Doctor?> GetByIdAsync(
        int docId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Doctors
            .AsNoTracking()
            .FirstOrDefaultAsync(doctor => doctor.DocId == docId, cancellationToken);
    }

    public async Task<int> CreateAsync(
        Doctor doctor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        doctor.DocName = doctor.DocName.Trim();
        doctor.Phone = Clean(doctor.Phone);
        doctor.Email = Clean(doctor.Email);
        doctor.IsActive = true;
        doctor.CreatedAt = DateTime.UtcNow;

        db.Doctors.Add(doctor);
        await db.SaveChangesAsync(cancellationToken);

        return doctor.DocId;
    }

    public async Task UpdateAsync(
        Doctor doctor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Doctors
            .FirstOrDefaultAsync(
                item => item.DocId == doctor.DocId,
                cancellationToken)
            ?? throw new InvalidOperationException("Doctor not found.");

        existing.DocName = doctor.DocName.Trim();
        existing.Phone = Clean(doctor.Phone);
        existing.Email = Clean(doctor.Email);
        existing.ModifiedAt = DateTime.UtcNow;
        existing.ModifiedBy = doctor.ModifiedBy;

        // Route active-state changes through the same tracked graph so disabling
        // the doctor and all related rates happen in a single SaveChanges call.
        if (existing.IsActive != doctor.IsActive)
        {
            existing.IsActive = doctor.IsActive;

            if (!doctor.IsActive)
            {
                var activeRates = await db.DoctorRates
                    .Where(rate => rate.DocId == doctor.DocId && rate.IsActive)
                    .ToListAsync(cancellationToken);

                foreach (var rate in activeRates)
                {
                    rate.IsActive = false;
                    rate.ModifiedAt = DateTime.UtcNow;
                    rate.ModifiedBy = doctor.ModifiedBy;
                }
            }
        }

        // Re-enabling a doctor deliberately does not re-enable old rates.
        // Admin chooses the required rates explicitly from the rate card.
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(
        int docId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var doctor = await db.Doctors
            .FirstOrDefaultAsync(
                item => item.DocId == docId,
                cancellationToken)
            ?? throw new InvalidOperationException("Doctor not found.");

        doctor.IsActive = isActive;
        doctor.ModifiedAt = DateTime.UtcNow;

        if (!isActive)
        {
            var activeRates = await db.DoctorRates
                .Where(rate => rate.DocId == docId && rate.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var rate in activeRates)
            {
                rate.IsActive = false;
                rate.ModifiedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
