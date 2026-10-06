CREATE PROCEDURE dbo.usp_GetActiveCaseTypes
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CaseTypeId, CaseTypeCode, CaseTypeName, Scope, DisplayOrder
    FROM dbo.CaseTypes
    WHERE IsActive = 1
    ORDER BY DisplayOrder, CaseTypeName;
END