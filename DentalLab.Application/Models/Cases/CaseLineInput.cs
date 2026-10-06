namespace DentalLab.Application.Models.Cases;

public sealed class CaseLineInput
{
    public Guid ClientLineId { get; set; } = Guid.NewGuid();
    public int CaseTypeId { get; set; }
    public string CaseTypeCode { get; set; } = string.Empty;
    public string CaseTypeName { get; set; } = string.Empty;
    public char Scope { get; set; } = 'T';
    public char? Arch { get; set; }
    public string? UR { get; set; }
    public string? UL { get; set; }
    public string? LR { get; set; }
    public string? LL { get; set; }
    public decimal UnitRate { get; set; }
    public byte[]? RowVersion { get; set; }
    public int Units => Scope == 'A' ? 1 : (UR?.Length ?? 0) + (UL?.Length ?? 0) + (LR?.Length ?? 0) + (LL?.Length ?? 0);
    public decimal Amount => Scope == 'A' ? UnitRate : Units * UnitRate;
    public string SelectionText
    {
        get
        {
            if (Scope == 'A') return Arch == 'U' ? "Upper Arch" : Arch == 'L' ? "Lower Arch" : "Arch not selected";
            var items = new List<string>();
            if (!string.IsNullOrWhiteSpace(UR)) items.Add($"UR {UR}");
            if (!string.IsNullOrWhiteSpace(UL)) items.Add($"UL {UL}");
            if (!string.IsNullOrWhiteSpace(LR)) items.Add($"LR {LR}");
            if (!string.IsNullOrWhiteSpace(LL)) items.Add($"LL {LL}");
            return items.Count == 0 ? "No teeth selected" : string.Join(" | ", items);
        }
    }
}
