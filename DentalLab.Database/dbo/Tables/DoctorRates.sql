CREATE TABLE [dbo].[DoctorRates] (
    [DocId]      INT             NOT NULL,
    [CaseTypeId] INT             NOT NULL,
    [Cost]       DECIMAL (10, 2) NOT NULL,
    [IsActive]   BIT             CONSTRAINT [DF_DoctorRates_IsActive] DEFAULT ((1)) NOT NULL,
    [CreatedAt]  DATETIME2 (0)   CONSTRAINT [DF_DoctorRates_CreatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedBy]  NVARCHAR (256)  NULL,
    [ModifiedAt] DATETIME2 (0)   NULL,
    [ModifiedBy] NVARCHAR (256)  NULL,
    CONSTRAINT [PK_DoctorRates] PRIMARY KEY CLUSTERED ([DocId] ASC, [CaseTypeId] ASC),
    CONSTRAINT [CK_DoctorRates_Cost] CHECK ([Cost]>=(0)),
    CONSTRAINT [FK_DoctorRates_CaseTypes] FOREIGN KEY ([CaseTypeId]) REFERENCES [dbo].[CaseTypes] ([CaseTypeId]),
    CONSTRAINT [FK_DoctorRates_Doctors] FOREIGN KEY ([DocId]) REFERENCES [dbo].[Doctors] ([DocId])
);


GO
CREATE NONCLUSTERED INDEX [IX_DoctorRates_CaseType]
    ON [dbo].[DoctorRates]([CaseTypeId] ASC, [IsActive] ASC)
    INCLUDE([DocId], [Cost]);

