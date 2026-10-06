CREATE PROCEDURE dbo.usp_GetRatesForDoctor
    @DocId INT,
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
    WHERE DocId = @DocId
      AND (@IncludeInactive = 1 OR IsRateActive = 1)
    ORDER BY DisplayOrder, CaseTypeName;
END