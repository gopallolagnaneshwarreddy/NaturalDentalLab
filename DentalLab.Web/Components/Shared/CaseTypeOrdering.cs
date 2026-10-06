using DentalLab.Application.Abstractions;

namespace DentalLab.Web.Components.Shared;

/// <summary>
/// Display order for new case types: always the next number in sequence
/// (highest existing order + 1, e.g. 18 -> 19).
/// The service's GetNextDisplayOrderAsync jumps by 10, which broke the sequence.
/// </summary>
public static class CaseTypeOrdering
{
    public static async Task<short> GetNextSequentialOrderAsync(this ICaseTypeService service)
    {
        // Include inactive case types so a hidden one never shares the same number.
        var all = await service.GetAllAsync(includeInactive: true);

        var max = all.Count == 0 ? 0 : all.Max(ct => (int)ct.DisplayOrder);

        return (short)Math.Min(max + 1, short.MaxValue);
    }
}
