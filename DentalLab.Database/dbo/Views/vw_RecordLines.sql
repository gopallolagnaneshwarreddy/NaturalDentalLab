CREATE VIEW dbo.vw_RecordLines
AS
SELECT
    r.CaseId,
    r.LineNumber,
    r.CaseDate,
    r.DocId,
    d.DocName,
    r.PatientName,
    r.PatientRef,
    r.CaseTypeId,
    ct.CaseTypeCode,
    ct.CaseTypeName,
    r.Scope,
    r.Arch,
    r.UR,
    r.UL,
    r.LR,
    r.LL,
    r.UnitRate,
    r.Units,
    r.Amount,
    r.IsCancelled,
    r.CancelReason,
    r.CreatedAt,
    r.CreatedBy,
    r.ModifiedAt,
    r.ModifiedBy
FROM dbo.Records r
JOIN dbo.Doctors d ON d.DocId = r.DocId
JOIN dbo.CaseTypes ct ON ct.CaseTypeId = r.CaseTypeId;