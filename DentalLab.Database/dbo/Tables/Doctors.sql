CREATE TABLE [dbo].[Doctors] (
    [DocId]      INT            IDENTITY (1, 1) NOT NULL,
    [DocName]    NVARCHAR (100) NOT NULL,
    [Phone]      VARCHAR (20)   NULL,
    [Email]      NVARCHAR (150) NULL,
    [IsActive]   BIT            CONSTRAINT [DF_Doctors_IsActive] DEFAULT ((1)) NOT NULL,
    [CreatedAt]  DATETIME2 (0)  CONSTRAINT [DF_Doctors_CreatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [CreatedBy]  NVARCHAR (256) NULL,
    [ModifiedAt] DATETIME2 (0)  NULL,
    [ModifiedBy] NVARCHAR (256) NULL,
    CONSTRAINT [PK_Doctors] PRIMARY KEY CLUSTERED ([DocId] ASC),
    CONSTRAINT [UQ_Doctors_DocName] UNIQUE NONCLUSTERED ([DocName] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_Doctors_ActiveName]
    ON [dbo].[Doctors]([IsActive] ASC, [DocName] ASC)
    INCLUDE([Phone], [Email]);

