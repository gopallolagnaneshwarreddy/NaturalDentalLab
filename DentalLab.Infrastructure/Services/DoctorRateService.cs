using DentalLab.Application.Abstractions;
using DentalLab.Application.Models;
using DentalLab.Domain.Entities;
using DentalLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Infrastructure.Services;

public sealed class DoctorRateService(
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : IDoctorRateService
{
    public async Task<List<DoctorRateListItem>> GetAllAsync(
        int? docId = null,
        int? caseTypeId = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.DoctorRates.AsNoTracking().AsQueryable();

        if (docId.HasValue)
        {
            query = query.Where(rate => rate.DocId == docId.Value);
        }

        if (caseTypeId.HasValue)
        {
            query = query.Where(rate => rate.CaseTypeId == caseTypeId.Value);
        }

        if (!includeInactive)
        {
            query = query.Where(rate =>
                rate.IsActive &&
                rate.Doctor.IsActive &&
                rate.CaseType.IsActive);
        }

        return await Project(query)
            .OrderBy(item => item.DocName)
            .ThenBy(item => item.DisplayOrder)
            .ThenBy(item => item.CaseTypeName)
            .ToListAsync(cancellationToken);
    }

    public Task<List<DoctorRateListItem>> GetForDoctorAsync(
        int docId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return GetAllAsync(
            docId,
            caseTypeId: null,
            includeInactive,
            cancellationToken);
    }

    public async Task<List<CaseType>> GetAvailableCaseTypesAsync(
        int docId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var doctorIsActive = await db.Doctors
            .AnyAsync(
                doctor => doctor.DocId == docId && doctor.IsActive,
                cancellationToken);

        if (!doctorIsActive)
        {
            return [];
        }

        return await db.CaseTypes
            .Where(caseType =>
                caseType.IsActive &&
                !db.DoctorRates.Any(rate =>
                    rate.DocId == docId &&
                    rate.CaseTypeId == caseType.CaseTypeId))
            .OrderBy(caseType => caseType.DisplayOrder)
            .ThenBy(caseType => caseType.CaseTypeName)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<DoctorRate?> GetAsync(
        int docId,
        int caseTypeId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.DoctorRates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                rate =>
                    rate.DocId == docId &&
                    rate.CaseTypeId == caseTypeId,
                cancellationToken);
    }

    public async Task CreateAsync(
        DoctorRate rate,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        await EnsureParentsAreActiveAsync(
            db,
            rate.DocId,
            rate.CaseTypeId,
            cancellationToken);

        rate.IsActive = true;
        rate.CreatedAt = DateTime.UtcNow;

        db.DoctorRates.Add(rate);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        DoctorRate rate,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.DoctorRates
            .FirstOrDefaultAsync(
                item =>
                    item.DocId == rate.DocId &&
                    item.CaseTypeId == rate.CaseTypeId,
                cancellationToken)
            ?? throw new InvalidOperationException("Doctor rate not found.");

        if (rate.IsActive)
        {
            await EnsureParentsAreActiveAsync(
                db,
                rate.DocId,
                rate.CaseTypeId,
                cancellationToken);
        }

        existing.Cost = rate.Cost;
        existing.IsActive = rate.IsActive;
        existing.ModifiedAt = DateTime.UtcNow;
        existing.ModifiedBy = rate.ModifiedBy;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(
        int docId,
        int caseTypeId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var rate = await db.DoctorRates
            .FirstOrDefaultAsync(
                item =>
                    item.DocId == docId &&
                    item.CaseTypeId == caseTypeId,
                cancellationToken)
            ?? throw new InvalidOperationException("Doctor rate not found.");

        if (isActive)
        {
            await EnsureParentsAreActiveAsync(
                db,
                docId,
                caseTypeId,
                cancellationToken);
        }

        // This changes only this Doctor + CaseType pair.
        rate.IsActive = isActive;
        rate.ModifiedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<DoctorRateListItem> Project(
        IQueryable<DoctorRate> query)
    {
        return query.Select(rate => new DoctorRateListItem
        {
            DocId = rate.DocId,
            DocName = rate.Doctor.DocName,
            CaseTypeId = rate.CaseTypeId,
            CaseTypeCode = rate.CaseType.CaseTypeCode,
            CaseTypeName = rate.CaseType.CaseTypeName,
            Scope = rate.CaseType.Scope,
            DisplayOrder = rate.CaseType.DisplayOrder,
            Cost = rate.Cost,
            IsActive = rate.IsActive
        });
    }

    private static async Task EnsureParentsAreActiveAsync(
        ApplicationDbContext db,
        int docId,
        int caseTypeId,
        CancellationToken cancellationToken)
    {
        var doctorIsActive = await db.Doctors
            .AnyAsync(
                doctor => doctor.DocId == docId && doctor.IsActive,
                cancellationToken);

        if (!doctorIsActive)
        {
            throw new InvalidOperationException(
                "The rate cannot be enabled because the doctor is inactive.");
        }

        var caseTypeIsActive = await db.CaseTypes
            .AnyAsync(
                caseType =>
                    caseType.CaseTypeId == caseTypeId &&
                    caseType.IsActive,
                cancellationToken);

        if (!caseTypeIsActive)
        {
            throw new InvalidOperationException(
                "The rate cannot be enabled because the case type is inactive.");
        }
    }
}
