-- Created automatically by the web app on start-up (DoctorLedgerStore). Kept here for reference.
CREATE TABLE [dbo].[DoctorPayments] (
    [PaymentId]   INT            IDENTITY (1, 1) NOT NULL,
    [DocId]       INT            NOT NULL,
    [PaymentDate] DATE           NOT NULL,
    [Amount]      DECIMAL (12, 2) NOT NULL,
    [Mode]        NVARCHAR (30)  NOT NULL,
    [Reference]   NVARCHAR (100) NULL,
    [Notes]       NVARCHAR (250) NULL,
    [IsVoided]    BIT            CONSTRAINT [DF_DoctorPayments_IsVoided] DEFAULT ((0)) NOT NULL,
    [VoidReason]  NVARCHAR (250) NULL,
    [VoidedAt]    DATETIME2 (0)  NULL,
    [VoidedBy]    NVARCHAR (256) NULL,
    [CreatedAt]   DATETIME2 (0)  CONSTRAINT [DF_DoctorPayments_CreatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedBy]   NVARCHAR (256) NOT NULL,
    CONSTRAINT [PK_DoctorPayments] PRIMARY KEY CLUSTERED ([PaymentId] ASC),
    CONSTRAINT [CK_DoctorPayments_Amount] CHECK ([Amount] > (0)),
    CONSTRAINT [FK_DoctorPayments_Doctors] FOREIGN KEY ([DocId]) REFERENCES [dbo].[Doctors] ([DocId])
);


GO
CREATE NONCLUSTERED INDEX [IX_DoctorPayments_DocDate]
    ON [dbo].[DoctorPayments]([DocId] ASC, [PaymentDate] ASC)
    INCLUDE([Amount], [IsVoided]);
