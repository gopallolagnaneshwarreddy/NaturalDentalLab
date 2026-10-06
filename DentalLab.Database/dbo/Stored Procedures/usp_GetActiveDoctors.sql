/* DentalLab - 08_Procedures.sql
   Read procedures support dynamically loaded dropdowns and administration lists. */

CREATE PROCEDURE dbo.usp_GetActiveDoctors
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DocId, DocName, Phone, Email
    FROM dbo.Doctors
    WHERE IsActive = 1
    ORDER BY DocName;
END