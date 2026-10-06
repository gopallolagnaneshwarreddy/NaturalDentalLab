CREATE TABLE [dbo].[CaseApprovalRequests] (
    [RequestId]           BIGINT          IDENTITY (1, 1) NOT NULL,
    [CaseId]              INT             NOT NULL,
    [RequestType]         VARCHAR (20)    NOT NULL,
    [Status]              VARCHAR (20)    NOT NULL,
    [RequestedByUserId]   NVARCHAR (450)  NOT NULL,
    [RequestedByEmail]    NVARCHAR (256)  NOT NULL,
    [AssignedAdminUserId] NVARCHAR (450)  NOT NULL,
    [AssignedAdminEmail]  NVARCHAR (256)  NOT NULL,
    [Reason]              NVARCHAR (500)  NOT NULL,
    [AdditionalDetails]   NVARCHAR (1000) NULL,
    [RequestedOn]         DATETIME2 (7)   NOT NULL,
    [ReviewedByUserId]    NVARCHAR (450)  NULL,
    [ReviewedByEmail]     NVARCHAR (256)  NULL,
    [ReviewedOn]          DATETIME2 (7)   NULL,
    [ReviewComment]       NVARCHAR (500)  NULL,
    [CompletedOn]         DATETIME2 (7)   NULL,
    [CompletedByUserId]   NVARCHAR (450)  NULL,
    [CompletedByEmail]    NVARCHAR (256)  NULL,
    [RowVersion]          ROWVERSION      NOT NULL,
    CONSTRAINT [PK_CaseApprovalRequests] PRIMARY KEY CLUSTERED ([RequestId] ASC),
    CONSTRAINT [CK_CaseApprovalRequests_Status] CHECK ([Status]='Withdrawn' OR [Status]='Completed' OR [Status]='Rejected' OR [Status]='Approved' OR [Status]='Pending'),
    CONSTRAINT [CK_CaseApprovalRequests_Type] CHECK ([RequestType]='Cancel' OR [RequestType]='Edit')
);


GO
CREATE NONCLUSTERED INDEX [IX_CaseApprovalRequests_CaseId]
    ON [dbo].[CaseApprovalRequests]([CaseId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CaseApprovalRequests_Requested_Status]
    ON [dbo].[CaseApprovalRequests]([RequestedByUserId] ASC, [Status] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CaseApprovalRequests_Assigned_Status]
    ON [dbo].[CaseApprovalRequests]([AssignedAdminUserId] ASC, [Status] ASC);

