/* DentalLab - 07_Views.sql */

CREATE VIEW dbo.vw_DoctorRates
AS
SELECT
    dr.DocId,
    d.DocName,
    dr.CaseTypeId,
    ct.CaseTypeCode,
    ct.CaseTypeName,
    ct.Scope,
    ct.DisplayOrder,
    dr.Cost,
    dr.IsActive AS IsRateActive,
    d.IsActive AS IsDoctorActive,
    ct.IsActive AS IsCaseTypeActive
FROM dbo.DoctorRates dr
JOIN dbo.Doctors d ON d.DocId = dr.DocId
JOIN dbo.CaseTypes ct ON ct.CaseTypeId = dr.CaseTypeId;