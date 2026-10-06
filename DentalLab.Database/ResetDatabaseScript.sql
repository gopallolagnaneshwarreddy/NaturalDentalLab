SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    ---------------------------------------------------------
    -- 1. BUSINESS DATA
    ---------------------------------------------------------

    DELETE FROM dbo.CaseApprovalRequests;
    DELETE FROM dbo.Records;
    DELETE FROM dbo.DoctorRates;
    DELETE FROM dbo.CaseTypes;
    DELETE FROM dbo.Doctors;
    DELETE FROM dbo.EmailNotifications

    ---------------------------------------------------------
    -- 2. ASP.NET CORE IDENTITY USER DATA
    ---------------------------------------------------------

    DELETE FROM dbo.AspNetUserTokens;
    DELETE FROM dbo.AspNetUserLogins;
    DELETE FROM dbo.AspNetUserClaims;
    DELETE FROM dbo.AspNetUserRoles;
    DELETE FROM dbo.AspNetUsers;

    ---------------------------------------------------------
    -- 3. REMOVE ROLES
    --
    -- IdentitySeeder will recreate Owner, Admin and Staff
    -- when the application starts.
    ---------------------------------------------------------

    DELETE FROM dbo.AspNetRoleClaims;
    DELETE FROM dbo.AspNetRoles;

    ---------------------------------------------------------
    -- 4. RESET BUSINESS IDENTITY COLUMNS
    ---------------------------------------------------------

    IF OBJECTPROPERTY(
        OBJECT_ID(N'dbo.CaseApprovalRequests'),
        'TableHasIdentity'
    ) = 1
    BEGIN
        DBCC CHECKIDENT
        (
            'dbo.CaseApprovalRequests',
            RESEED,
            0
        );
    END;

    IF OBJECTPROPERTY(
        OBJECT_ID(N'dbo.Doctors'),
        'TableHasIdentity'
    ) = 1
    BEGIN
        DBCC CHECKIDENT
        (
            'dbo.Doctors',
            RESEED,
            0
        );
    END;

    IF OBJECTPROPERTY(
        OBJECT_ID(N'dbo.CaseTypes'),
        'TableHasIdentity'
    ) = 1
    BEGIN
        DBCC CHECKIDENT
        (
            'dbo.CaseTypes',
            RESEED,
            0
        );
    END;

    ---------------------------------------------------------
    -- 5. RESET CASE ID SEQUENCE
    ---------------------------------------------------------

    IF OBJECT_ID(N'dbo.Seq_CaseId', N'SO') IS NOT NULL
    BEGIN
        ALTER SEQUENCE dbo.Seq_CaseId
        RESTART WITH 1;
    END;

    COMMIT TRANSACTION;

    PRINT 'Dental Lab database reset completed successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;