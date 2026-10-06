-- Created automatically by the web app on start-up (DoctorLedgerStore). Kept here for reference.
-- Amount each doctor still owed on the dues tracking start date (AppSettings 'Ledger.StartDate').
CREATE TABLE [dbo].[DoctorOpeningBalances] (
    [DocId]      INT             NOT NULL,
    [Amount]     DECIMAL (12, 2) NOT NULL,
    [ModifiedAt] DATETIME2 (0)   CONSTRAINT [DF_DoctorOpeningBalances_ModifiedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [ModifiedBy] NVARCHAR (256)  NULL,
    CONSTRAINT [PK_DoctorOpeningBalances] PRIMARY KEY CLUSTERED ([DocId] ASC),
    CONSTRAINT [FK_DoctorOpeningBalances_Doctors] FOREIGN KEY ([DocId]) REFERENCES [dbo].[Doctors] ([DocId])
);
