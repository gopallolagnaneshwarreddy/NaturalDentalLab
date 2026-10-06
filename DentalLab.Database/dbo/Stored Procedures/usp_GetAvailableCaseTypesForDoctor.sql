CREATE PROCEDURE dbo.usp_GetAvailableCaseTypesForDoctor
    @DocId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ct.CaseTypeId,
        ct.CaseTypeCode,
        ct.CaseTypeName,
        ct.Scope,
        ct.DisplayOrder
    FROM dbo.CaseTypes ct
    WHERE ct.IsActive = 1
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.DoctorRates dr
          WHERE dr.DocId = @DocId
            AND dr.CaseTypeId = ct.CaseTypeId
      )
    ORDER BY ct.DisplayOrder, ct.CaseTypeName;
END