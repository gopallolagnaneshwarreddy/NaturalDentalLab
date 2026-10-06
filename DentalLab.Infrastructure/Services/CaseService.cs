using DentalLab.Application.Abstractions;
using DentalLab.Application.Models.Cases;
using DentalLab.Application.Models.Common;
using DentalLab.Domain.Entities;
using DentalLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Infrastructure.Services;

public sealed class CaseService(IDbContextFactory<ApplicationDbContext> factory) : ICaseService
{
    public async Task<CaseSaveResult> CreateAsync(CreateCaseRequest request, string user, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request, user, ct);
        if (!validation.Result.Succeeded) return validation.Result;
        await using var db = await factory.CreateDbContextAsync(ct);
        var caseId = await db.NextCaseIdAsync(ct);
        byte number = 1;
        foreach (var input in request.Lines)
        {
            var rate = validation.Rates![input.CaseTypeId];
            var row = new Record { CaseId = caseId, LineNumber = number++, CreatedAt = DateTime.UtcNow, CreatedBy = user };
            Apply(row, request, input, rate, user, false);
            db.Records.Add(row);
        }
        try { await db.SaveChangesAsync(ct); return CaseSaveResult.Success(caseId); }
        catch (DbUpdateException ex) { return CaseSaveResult.Failure(ex.InnerException?.Message ?? "The case could not be saved."); }
    }

    public async Task<PagedResult<CaseListItem>> SearchAsync(CaseFilter filter, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Records.AsNoTracking().AsQueryable();
        if (filter.CaseId.HasValue) query = query.Where(x => x.CaseId == filter.CaseId);
        if (filter.DocId.HasValue) query = query.Where(x => x.DocId == filter.DocId);
        if (filter.FromDate.HasValue) query = query.Where(x => x.CaseDate >= filter.FromDate);
        if (filter.ToDate.HasValue) query = query.Where(x => x.CaseDate <= filter.ToDate);
        if (!string.IsNullOrWhiteSpace(filter.Patient))
        {
            var text = filter.Patient.Trim();
            query = query.Where(x => (x.PatientName != null && x.PatientName.Contains(text)) || (x.PatientRef != null && x.PatientRef.Contains(text)));
        }
        if (filter.Status == "Active") query = query.Where(x => !x.IsCancelled);
        else if (filter.Status == "Cancelled") query = query.Where(x => x.IsCancelled);

        var pageSize = filter.PageSize is 10 or 20 or 50 or 100 ? filter.PageSize : 20;
        var page = Math.Max(1, filter.PageNumber);
        var grouped = query.GroupBy(x => x.CaseId).Select(g => new
        {
            CaseId = g.Key,
            Date = g.Min(x => x.CaseDate),
            DoctorName = g.First().Doctor.DocName,
            PatientName = g.First().PatientName,
            IsCancelled = g.All(x => x.IsCancelled),
            CreatedBy = g.First().CreatedBy
        });
        var count = await grouped.CountAsync(ct);

        var ordered = filter.SortColumn switch
        {
            "CaseId" => filter.SortDescending ? grouped.OrderByDescending(x => x.CaseId) : grouped.OrderBy(x => x.CaseId),
            "Doctor" => filter.SortDescending ? grouped.OrderByDescending(x => x.DoctorName) : grouped.OrderBy(x => x.DoctorName),
            "Patient" => filter.SortDescending ? grouped.OrderByDescending(x => x.PatientName) : grouped.OrderBy(x => x.PatientName),
            "Status" => filter.SortDescending ? grouped.OrderByDescending(x => x.IsCancelled) : grouped.OrderBy(x => x.IsCancelled),
            "CreatedBy" => filter.SortDescending ? grouped.OrderByDescending(x => x.CreatedBy) : grouped.OrderBy(x => x.CreatedBy),
            _ => filter.SortDescending ? grouped.OrderByDescending(x => x.Date) : grouped.OrderBy(x => x.Date)
        };

        var ids = await ordered.ThenByDescending(x => x.CaseId)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.CaseId).ToListAsync(ct);

        var rows = await db.Records.AsNoTracking().Where(x => ids.Contains(x.CaseId))
            .Select(x => new { x.CaseId, x.CaseDate, x.DocId, DoctorName=x.Doctor.DocName, x.PatientName, x.PatientRef,
                x.IsCancelled, x.CancelReason, x.CreatedAt, x.CreatedBy, x.ModifiedAt, x.ModifiedBy, x.LineNumber,
                x.CaseTypeId, x.CaseType.CaseTypeCode, x.CaseType.CaseTypeName, x.Scope, x.Arch, x.UR, x.UL, x.LR, x.LL,
                x.UnitRate, x.Units, x.Amount, x.RowVersion }).ToListAsync(ct);

        var items = rows.GroupBy(x => x.CaseId).Select(g => new CaseListItem
        {
            CaseId=g.Key, CaseDate=g.Min(x=>x.CaseDate), DocId=g.First().DocId, DoctorName=g.First().DoctorName,
            PatientName=g.First().PatientName, PatientRef=g.First().PatientRef, IsCancelled=g.All(x=>x.IsCancelled),
            CancelReason=g.First().CancelReason, CreatedAt=g.Min(x=>x.CreatedAt), CreatedBy=g.First().CreatedBy,
            ModifiedAt=g.Max(x=>x.ModifiedAt), ModifiedBy=g.OrderByDescending(x=>x.ModifiedAt).First().ModifiedBy,
            TotalUnits=g.Sum(x=>x.Units), TotalAmount=g.Sum(x=>x.Amount),
            Lines=g.OrderBy(x=>x.LineNumber).Select(x=>Line(x.LineNumber,x.CaseTypeId,x.CaseTypeCode,x.CaseTypeName,x.Scope,x.Arch,x.UR,x.UL,x.LR,x.LL,x.UnitRate,x.Units,x.Amount,x.RowVersion)).ToList()
        }).ToList();

        // Preserve the sort order already applied at the database level (by CaseId order from `ids`),
        // rather than re-sorting here, so the chosen SortColumn/SortDescending is respected.
        var itemsByCaseId = items.ToDictionary(x => x.CaseId);
        var sortedItems = ids.Where(itemsByCaseId.ContainsKey).Select(id => itemsByCaseId[id]).ToList();

        return new PagedResult<CaseListItem>{ Items=sortedItems, PageNumber=page, PageSize=pageSize, TotalCount=count };
    }

    public async Task<CaseDetails?> GetByIdAsync(int caseId, CancellationToken ct = default)
    {
        var all = await SearchAsync(new CaseFilter { CaseId=caseId, Status="All", PageSize=10 }, ct);
        var item = all.Items.FirstOrDefault();
        if (item is null) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        var notes = await db.Records.AsNoTracking().Where(x=>x.CaseId==caseId).Select(x=>x.Notes).FirstAsync(ct);
        return new CaseDetails { CaseId=item.CaseId, CaseDate=item.CaseDate, DocId=item.DocId, DoctorName=item.DoctorName,
            PatientName=item.PatientName, PatientRef=item.PatientRef, Notes=notes, IsCancelled=item.IsCancelled,
            CancelReason=item.CancelReason, CreatedAt=item.CreatedAt, CreatedBy=item.CreatedBy,
            ModifiedAt=item.ModifiedAt, ModifiedBy=item.ModifiedBy, Lines=item.Lines };
    }

    public async Task<CaseSaveResult> UpdateAsync(int caseId, CreateCaseRequest request, string user, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request,user,ct); if(!validation.Result.Succeeded)return validation.Result;
        await using var db=await factory.CreateDbContextAsync(ct);
        var existing=await db.Records.Where(x=>x.CaseId==caseId).OrderBy(x=>x.LineNumber).ToListAsync(ct);
        if(existing.Count==0)return CaseSaveResult.Failure("Case not found.");
        if(existing.Any(x=>x.IsCancelled))return CaseSaveResult.Failure("A cancelled case cannot be edited.");
        byte no=1;
        foreach(var input in request.Lines)
        {
            var rate=validation.Rates![input.CaseTypeId];
            var row=existing.FirstOrDefault(x=>x.LineNumber==no);
            if(row is null){row=new Record{CaseId=caseId,LineNumber=no,CreatedAt=DateTime.UtcNow,CreatedBy=user};db.Records.Add(row);}
            else
            {
                if(input.RowVersion is null||input.RowVersion.Length==0)return CaseSaveResult.Failure("Concurrency token missing. Refresh and try again.");
                db.Entry(row).Property(x=>x.RowVersion).OriginalValue=input.RowVersion;
            }
            Apply(row,request,input,rate,user,true); no++;
        }
        foreach(var extra in existing.Where(x=>x.LineNumber>=no))db.Records.Remove(extra);
        try{await db.SaveChangesAsync(ct);return CaseSaveResult.Success(caseId);}
        catch(DbUpdateConcurrencyException){return CaseSaveResult.Failure("This case was changed by another user. Refresh and review the latest values.");}
        catch(DbUpdateException ex){return CaseSaveResult.Failure(ex.InnerException?.Message??"The case could not be updated.");}
    }

    public async Task<CaseSaveResult> CancelAsync(int caseId,string reason,string user,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(reason))return CaseSaveResult.Failure("Cancellation reason is required.");
        await using var db=await factory.CreateDbContextAsync(ct);
        var rows=await db.Records.Where(x=>x.CaseId==caseId).ToListAsync(ct);
        if(rows.Count==0||rows.All(x=>x.IsCancelled))return CaseSaveResult.Failure("Case not found or already cancelled.");
        foreach(var row in rows){row.IsCancelled=true;row.CancelReason=reason.Trim();row.ModifiedAt=DateTime.UtcNow;row.ModifiedBy=user;}
        try{await db.SaveChangesAsync(ct);return CaseSaveResult.Success(caseId);}
        catch(DbUpdateConcurrencyException){return CaseSaveResult.Failure("This case was changed by another user. Refresh and try again.");}
    }

    private async Task<(CaseSaveResult Result,Dictionary<int,RateInfo>? Rates)> ValidateAsync(CreateCaseRequest request,string user,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(user))return(CaseSaveResult.Failure("Current user unavailable."),null);
        if(request.DocId<=0)return(CaseSaveResult.Failure("Select a doctor."),null);
        if(request.CaseDate>DateOnly.FromDateTime(DateTime.Today))return(CaseSaveResult.Failure("Case date cannot be in the future."),null);
        if(request.Lines.Count==0)return(CaseSaveResult.Failure("Add at least one case line."),null);
        var duplicate=Duplicate(request.Lines);if(duplicate!=null)return(CaseSaveResult.Failure($"Tooth {duplicate} appears in more than one line."),null);
        await using var db=await factory.CreateDbContextAsync(ct);
        if(!await db.Doctors.AnyAsync(x=>x.DocId==request.DocId&&x.IsActive,ct))return(CaseSaveResult.Failure("Doctor is inactive or unavailable."),null);
        var ids=request.Lines.Select(x=>x.CaseTypeId).Distinct().ToList();
        var rates=await db.DoctorRates.Where(x=>x.DocId==request.DocId&&x.IsActive&&x.Doctor.IsActive&&x.CaseType.IsActive&&ids.Contains(x.CaseTypeId))
            .Select(x=>new RateInfo(x.CaseTypeId,x.Cost,x.CaseType.Scope)).ToDictionaryAsync(x=>x.Id,ct);
        foreach(var line in request.Lines){if(!rates.TryGetValue(line.CaseTypeId,out var rate))return(CaseSaveResult.Failure($"No active rate for {line.CaseTypeName}."),null);if(rate.Scope=='T'&&line.Units==0)return(CaseSaveResult.Failure($"Select teeth for {line.CaseTypeName}."),null);if(rate.Scope=='A'&&line.Arch is not('U'or'L'))return(CaseSaveResult.Failure($"Select an arch for {line.CaseTypeName}."),null);}
        return(CaseSaveResult.Success(0),rates);
    }

    private static void Apply(Record row,CreateCaseRequest req,CaseLineInput input,RateInfo rate,string user,bool modified)
    {row.CaseDate=req.CaseDate;row.DocId=req.DocId;row.PatientName=Clean(req.PatientName);row.PatientRef=Clean(req.PatientRef);row.CaseTypeId=input.CaseTypeId;row.Scope=rate.Scope;row.Arch=rate.Scope=='A'?input.Arch:null;row.UR=rate.Scope=='T'?Norm(input.UR,true):null;row.UL=rate.Scope=='T'?Norm(input.UL,false):null;row.LR=rate.Scope=='T'?Norm(input.LR,true):null;row.LL=rate.Scope=='T'?Norm(input.LL,false):null;row.UnitRate=rate.Cost;row.Notes=Clean(req.Notes);row.IsCancelled=false;row.CancelReason=null;if(modified){row.ModifiedAt=DateTime.UtcNow;row.ModifiedBy=user;}}
    private static CaseLineView Line(byte no,int id,string code,string name,char scope,char? arch,string? ur,string? ul,string? lr,string? ll,decimal rate,int units,decimal amount,byte[] rv)=>new(){LineNumber=no,CaseTypeId=id,CaseTypeCode=code,CaseTypeName=name,Scope=scope,Arch=arch,UR=ur,UL=ul,LR=lr,LL=ll,UnitRate=rate,Units=units,Amount=amount,RowVersion=rv};
    private static string? Duplicate(IEnumerable<CaseLineInput> lines){var used=new HashSet<string>();foreach(var l in lines.Where(x=>x.Scope=='T'))foreach(var p in new[]{("UR",l.UR),("UL",l.UL),("LR",l.LR),("LL",l.LL)})foreach(var t in(p.Item2??"").Where(x=>x is>='1'and<='8'))if(!used.Add($"{p.Item1}-{t}"))return $"{p.Item1}-{t}";return null;}
    private static string? Norm(string? v,bool desc){if(string.IsNullOrWhiteSpace(v))return null;var c=v.Where(x=>x is>='1'and<='8').Distinct();c=desc?c.OrderByDescending(x=>x):c.OrderBy(x=>x);var r=string.Concat(c);return r.Length==0?null:r;}
    private static string? Clean(string? v)=>string.IsNullOrWhiteSpace(v)?null:v.Trim();
    private sealed record RateInfo(int Id,decimal Cost,char Scope);
}
