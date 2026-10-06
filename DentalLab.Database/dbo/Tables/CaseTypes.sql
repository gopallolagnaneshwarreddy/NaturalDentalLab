CREATE TABLE [dbo].[CaseTypes] (
    [CaseTypeId]   INT            IDENTITY (1, 1) NOT NULL,
    [CaseTypeCode] VARCHAR (20)   NOT NULL,
    [CaseTypeName] NVARCHAR (100) NOT NULL,
    [Scope]        CHAR (1)       NOT NULL,
    [DisplayOrder] SMALLINT       CONSTRAINT [DF_CaseTypes_DisplayOrder] DEFAULT ((100)) NOT NULL,
    [IsActive]     BIT            CONSTRAINT [DF_CaseTypes_IsActive] DEFAULT ((1)) NOT NULL,
    [CreatedAt]    DATETIME2 (0)  CONSTRAINT [DF_CaseTypes_CreatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedBy]    NVARCHAR (256) NULL,
    [ModifiedAt]   DATETIME2 (0)  NULL,
    [ModifiedBy]   NVARCHAR (256) NULL,
    CONSTRAINT [PK_CaseTypes] PRIMARY KEY CLUSTERED ([CaseTypeId] ASC),
    CONSTRAINT [CK_CaseTypes_CodeFormat] CHECK (([CaseTypeCode]) collate Latin1_General_100_BIN2=(upper(ltrim(rtrim([CaseTypeCode])))) collate Latin1_General_100_BIN2 AND NOT [CaseTypeCode] like '%[^A-Z0-9 _-]%'),
    CONSTRAINT [CK_CaseTypes_CodeNotBlank] CHECK (len(ltrim(rtrim([CaseTypeCode])))>(0)),
    CONSTRAINT [CK_CaseTypes_NameNotBlank] CHECK (len(ltrim(rtrim([CaseTypeName])))>(0)),
    CONSTRAINT [CK_CaseTypes_Scope] CHECK ([Scope]='A' OR [Scope]='T'),
    CONSTRAINT [UQ_CaseTypes_Code] UNIQUE NONCLUSTERED ([CaseTypeCode] ASC),
    CONSTRAINT [UQ_CaseTypes_IdScope] UNIQUE NONCLUSTERED ([CaseTypeId] ASC, [Scope] ASC),
    CONSTRAINT [UQ_CaseTypes_Name] UNIQUE NONCLUSTERED ([CaseTypeName] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_CaseTypes_ActiveOrder]
    ON [dbo].[CaseTypes]([IsActive] ASC, [DisplayOrder] ASC, [CaseTypeName] ASC)
    INCLUDE([CaseTypeCode], [Scope]);

