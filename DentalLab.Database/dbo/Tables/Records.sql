CREATE TABLE [dbo].[Records] (
    [CaseId]       INT             NOT NULL,
    [LineNumber]   TINYINT         CONSTRAINT [DF_Records_LineNumber] DEFAULT ((1)) NOT NULL,
    [CaseDate]     DATE            NOT NULL,
    [DocId]        INT             NOT NULL,
    [PatientName]  NVARCHAR (100)  NULL,
    [PatientRef]   NVARCHAR (50)   NULL,
    [CaseTypeId]   INT             NOT NULL,
    [Scope]        CHAR (1)        NOT NULL,
    [Arch]         CHAR (1)        NULL,
    [UR]           VARCHAR (8)     NULL,
    [UL]           VARCHAR (8)     NULL,
    [LR]           VARCHAR (8)     NULL,
    [LL]           VARCHAR (8)     NULL,
    [UnitRate]     DECIMAL (10, 2) NOT NULL,
    [Units]        AS              (case when [Scope]='A' then (1) else ((len(isnull([UR],''))+len(isnull([UL],'')))+len(isnull([LR],'')))+len(isnull([LL],'')) end) PERSISTED,
    [Amount]       AS              (CONVERT([decimal](12,2),case when [Scope]='A' then [UnitRate] else (((len(isnull([UR],''))+len(isnull([UL],'')))+len(isnull([LR],'')))+len(isnull([LL],'')))*[UnitRate] end)) PERSISTED,
    [Notes]        NVARCHAR (250)  NULL,
    [IsCancelled]  BIT             CONSTRAINT [DF_Records_IsCancelled] DEFAULT ((0)) NOT NULL,
    [CancelReason] NVARCHAR (250)  NULL,
    [CreatedAt]    DATETIME2 (0)   CONSTRAINT [DF_Records_CreatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedBy]    NVARCHAR (256)  NOT NULL,
    [ModifiedAt]   DATETIME2 (0)   NULL,
    [ModifiedBy]   NVARCHAR (256)  NULL,
    [RowVersion]   ROWVERSION      NOT NULL,
    CONSTRAINT [PK_Records] PRIMARY KEY CLUSTERED ([CaseId] ASC, [LineNumber] ASC),
    CONSTRAINT [CK_Records_Arch] CHECK ([Arch] IS NULL OR ([Arch]='L' OR [Arch]='U')),
    CONSTRAINT [CK_Records_CancelReason] CHECK ([IsCancelled]=(0) AND [CancelReason] IS NULL OR [IsCancelled]=(1) AND len(ltrim(rtrim([CancelReason])))>(0)),
    CONSTRAINT [CK_Records_LineNumber] CHECK ([LineNumber]>=(1)),
    CONSTRAINT [CK_Records_LL] CHECK ([LL] IS NULL OR len([LL])>(0) AND NOT [LL] like '%[^1-8]%'),
    CONSTRAINT [CK_Records_LR] CHECK ([LR] IS NULL OR len([LR])>(0) AND NOT [LR] like '%[^1-8]%'),
    CONSTRAINT [CK_Records_Scope] CHECK ([Scope]='A' OR [Scope]='T'),
    CONSTRAINT [CK_Records_Shape] CHECK ([Scope]='A' AND ([Arch]='L' OR [Arch]='U') AND [UR] IS NULL AND [UL] IS NULL AND [LR] IS NULL AND [LL] IS NULL OR [Scope]='T' AND [Arch] IS NULL AND coalesce([UR],[UL],[LR],[LL]) IS NOT NULL),
    CONSTRAINT [CK_Records_UL] CHECK ([UL] IS NULL OR len([UL])>(0) AND NOT [UL] like '%[^1-8]%'),
    CONSTRAINT [CK_Records_UnitRate] CHECK ([UnitRate]>=(0)),
    CONSTRAINT [CK_Records_UR] CHECK ([UR] IS NULL OR len([UR])>(0) AND NOT [UR] like '%[^1-8]%'),
    CONSTRAINT [FK_Records_CaseTypes] FOREIGN KEY ([CaseTypeId], [Scope]) REFERENCES [dbo].[CaseTypes] ([CaseTypeId], [Scope]),
    CONSTRAINT [FK_Records_DoctorRates] FOREIGN KEY ([DocId], [CaseTypeId]) REFERENCES [dbo].[DoctorRates] ([DocId], [CaseTypeId]),
    CONSTRAINT [FK_Records_Doctors] FOREIGN KEY ([DocId]) REFERENCES [dbo].[Doctors] ([DocId])
);


GO
CREATE NONCLUSTERED INDEX [IX_Records_DoctorDate]
    ON [dbo].[Records]([DocId] ASC, [CaseDate] ASC)
    INCLUDE([CaseId], [LineNumber], [CaseTypeId], [Scope], [Units], [Amount], [IsCancelled]);


GO
CREATE NONCLUSTERED INDEX [IX_Records_CaseTypeDate]
    ON [dbo].[Records]([CaseTypeId] ASC, [CaseDate] ASC)
    INCLUDE([DocId], [Units], [Amount], [IsCancelled]);


GO
CREATE NONCLUSTERED INDEX [IX_Records_PatientRef]
    ON [dbo].[Records]([PatientRef] ASC)
    INCLUDE([CaseId], [DocId], [CaseDate], [PatientName]) WHERE ([PatientRef] IS NOT NULL);

