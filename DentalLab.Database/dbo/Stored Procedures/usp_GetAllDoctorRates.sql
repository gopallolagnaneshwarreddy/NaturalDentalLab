CREATE PROCEDURE dbo.usp_GetAllDoctorRates
    @DocId INT = NULL,
    @CaseTypeId INT = NULL,
    @IncludeInactive BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        DocId,
        DocName,
        CaseTypeId,
        CaseTypeCode,
        CaseTypeName,
        Scope,
        Cost,
        IsRateActive
    FROM dbo.vw_DoctorRates
    WHERE (@DocId IS NULL OR DocId = @DocId)
      AND (@CaseTypeId IS NULL OR CaseTypeId = @CaseTypeId)
      AND (@IncludeInactive = 1 OR IsRateActive = 1)
    ORDER BY DocName, DisplayOrder, CaseTypeName;
END