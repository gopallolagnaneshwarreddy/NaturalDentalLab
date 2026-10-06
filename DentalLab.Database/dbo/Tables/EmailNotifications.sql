CREATE TABLE [dbo].[EmailNotifications] (
    [EmailNotificationId] BIGINT          IDENTITY (1, 1) NOT NULL,
    [RecipientEmail]      NVARCHAR (256)  NOT NULL,
    [Subject]             NVARCHAR (300)  NOT NULL,
    [NotificationType]    NVARCHAR (50)   NOT NULL,
    [RelatedEntityType]   NVARCHAR (50)   NULL,
    [RelatedEntityId]     NVARCHAR (100)  NULL,
    [Status]              NVARCHAR (20)   NOT NULL,
    [AttemptCount]        INT             NOT NULL,
    [ErrorMessage]        NVARCHAR (2000) NULL,
    [CreatedOn]           DATETIME2 (7)   NOT NULL,
    [SentOn]              DATETIME2 (7)   NULL,
    PRIMARY KEY CLUSTERED ([EmailNotificationId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_EmailNotifications_Status_CreatedOn]
    ON [dbo].[EmailNotifications]([Status] ASC, [CreatedOn] ASC);

