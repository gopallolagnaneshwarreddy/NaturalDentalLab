CREATE VIEW dbo.vw_Cases
AS
SELECT
    r.CaseId,
    r.DocId,
    MIN(d.DocName) AS DocName,
    MIN(r.CaseDate) AS CaseDate,
    MIN(r.PatientName) AS PatientName,
    MIN(r.PatientRef) AS PatientRef,
    STRING_AGG(ct.CaseTypeCode, '/') WITHIN GROUP (ORDER BY r.LineNumber) AS CaseTypeText,
    SUM(r.Units) AS TotalUnits,
    SUM(r.Amount) AS TotalAmount,
    COUNT(*) AS LineCount,
    MAX(CONVERT(TINYINT, r.IsCancelled)) AS IsCancelled
FROM dbo.Records r
JOIN dbo.Doctors d ON d.DocId = r.DocId
JOIN dbo.CaseTypes ct ON ct.CaseTypeId = r.CaseTypeId
GROUP BY r.CaseId, r.DocId;