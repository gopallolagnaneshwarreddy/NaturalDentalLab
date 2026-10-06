-- Created automatically by the web app on start-up (DoctorLedgerStore). Kept here for reference.
-- Bills saved to the ledger from the Doctor Bill page; their GST and adjustments carry forward.
CREATE TABLE [dbo].[DoctorBills] (
    [BillId]           INT             IDENTITY (1, 1) NOT NULL,
    [DocId]            INT             NOT NULL,
    [PeriodFrom]       DATE            NOT NULL,
    [PeriodTo]         DATE            NOT NULL,
    [Subtotal]         DECIMAL (12, 2) NOT NULL,
    [GstAmount]        DECIMAL (12, 2) NOT NULL,
    [AdjustmentsTotal] DECIMAL (12, 2) NOT NULL,
    [AdjustmentsText]  NVARCHAR (1000) NULL,
    [OldDue]           DECIMAL (12, 2) NOT NULL,
    [GrandTotal]       DECIMAL (12, 2) NOT NULL,
    [SavedAt]          DATETIME2 (0)   CONSTRAINT [DF_DoctorBills_SavedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [SavedBy]          NVARCHAR (256)  NOT NULL,
    CONSTRAINT [PK_DoctorBills] PRIMARY KEY CLUSTERED ([BillId] ASC),
    CONSTRAINT [UQ_DoctorBills_DocPeriod] UNIQUE NONCLUSTERED ([DocId] ASC, [PeriodFrom] ASC, [PeriodTo] ASC),
    CONSTRAINT [FK_DoctorBills_Doctors] FOREIGN KEY ([DocId]) REFERENCES [dbo].[Doctors] ([DocId])
);
