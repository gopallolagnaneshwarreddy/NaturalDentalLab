using DentalLab.Application.Abstractions;
using DentalLab.Domain.Entities;
using DentalLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Infrastructure.Services;

public sealed class CaseTypeService(
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : ICaseTypeService
{
    public async Task<List<CaseType>> GetAllAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.CaseTypes
            .Where(caseType => includeInactive || caseType.IsActive)
            .OrderBy(caseType => caseType.DisplayOrder)
            .ThenBy(caseType => caseType.CaseTypeName)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<CaseType?> GetByIdAsync(
        int caseTypeId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.CaseTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                caseType => caseType.CaseTypeId == caseTypeId,
                cancellationToken);
    }

    public async Task<short> GetNextDisplayOrderAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var maximum = await db.CaseTypes
            .Select(caseType => (short?)caseType.DisplayOrder)
            .MaxAsync(cancellationToken) ?? 0;

        return checked((short)(maximum + 10));
    }

    public async Task<int> CreateAsync(
        CaseType caseType,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        caseType.CaseTypeCode = caseType.CaseTypeCode.Trim().ToUpperInvariant();
        caseType.CaseTypeName = caseType.CaseTypeName.Trim();
        caseType.IsActive = true;
        caseType.CreatedAt = DateTime.UtcNow;

        db.CaseTypes.Add(caseType);
        await db.SaveChangesAsync(cancellationToken);

        return caseType.CaseTypeId;
    }

    public async Task UpdateAsync(
        CaseType caseType,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.CaseTypes
            .FirstOrDefaultAsync(
                item => item.CaseTypeId == caseType.CaseTypeId,
                cancellationToken)
            ?? throw new InvalidOperationException("Case type not found.");

        existing.CaseTypeCode = caseType.CaseTypeCode.Trim().ToUpperInvariant();
        existing.CaseTypeName = caseType.CaseTypeName.Trim();
        existing.Scope = caseType.Scope;
        existing.DisplayOrder = caseType.DisplayOrder;
        existing.ModifiedAt = DateTime.UtcNow;
        existing.ModifiedBy = caseType.ModifiedBy;

        if (existing.IsActive != caseType.IsActive)
        {
            existing.IsActive = caseType.IsActive;

            if (!caseType.IsActive)
            {
                var activeRates = await db.DoctorRates
                    .Where(rate =>
                        rate.CaseTypeId == caseType.CaseTypeId &&
                        rate.IsActive)
                    .ToListAsync(cancellationToken);

                foreach (var rate in activeRates)
                {
                    rate.IsActive = false;
                    rate.ModifiedAt = DateTime.UtcNow;
                    rate.ModifiedBy = caseType.ModifiedBy;
                }
            }
        }

        // Re-enabling a case type does not revive doctor rates automatically.
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(
        int caseTypeId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var caseType = await db.CaseTypes
            .FirstOrDefaultAsync(
                item => item.CaseTypeId == caseTypeId,
                cancellationToken)
            ?? throw new InvalidOperationException("Case type not found.");

        caseType.IsActive = isActive;
        caseType.ModifiedAt = DateTime.UtcNow;

        if (!isActive)
        {
            var activeRates = await db.DoctorRates
                .Where(rate => rate.CaseTypeId == caseTypeId && rate.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var rate in activeRates)
            {
                rate.IsActive = false;
                rate.ModifiedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
