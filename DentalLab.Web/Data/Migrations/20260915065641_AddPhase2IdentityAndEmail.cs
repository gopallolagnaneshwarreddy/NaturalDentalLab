using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalLab.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2IdentityAndEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'dbo.AspNetUsers',
                    'MustChangePassword'
                ) IS NULL
                BEGIN
                    ALTER TABLE dbo.AspNetUsers
                    ADD MustChangePassword bit NOT NULL
                        CONSTRAINT DF_AspNetUsers_MustChangePassword
                        DEFAULT (0);
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'dbo.AspNetUsers',
                    'TemporaryPasswordIssuedOn'
                ) IS NULL
                BEGIN
                    ALTER TABLE dbo.AspNetUsers
                    ADD TemporaryPasswordIssuedOn datetime2 NULL;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'dbo.AspNetUsers',
                    'PasswordChangedOn'
                ) IS NULL
                BEGIN
                    ALTER TABLE dbo.AspNetUsers
                    ADD PasswordChangedOn datetime2 NULL;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'dbo.AspNetUsers',
                    'InvitationEmailStatus'
                ) IS NULL
                BEGIN
                    ALTER TABLE dbo.AspNetUsers
                    ADD InvitationEmailStatus nvarchar(20) NOT NULL
                        CONSTRAINT DF_AspNetUsers_InvitationEmailStatus
                        DEFAULT (N'NotSent');
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'dbo.AspNetUsers',
                    'InvitationEmailSentOn'
                ) IS NULL
                BEGIN
                    ALTER TABLE dbo.AspNetUsers
                    ADD InvitationEmailSentOn datetime2 NULL;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'dbo.AspNetUsers',
                    'InvitationEmailError'
                ) IS NULL
                BEGIN
                    ALTER TABLE dbo.AspNetUsers
                    ADD InvitationEmailError nvarchar(2000) NULL;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(
                    N'dbo.EmailNotifications',
                    N'U'
                ) IS NULL
                BEGIN
                    CREATE TABLE dbo.EmailNotifications
                    (
                        EmailNotificationId bigint
                            IDENTITY(1,1) NOT NULL,

                        RecipientEmail nvarchar(256)
                            NOT NULL,

                        Subject nvarchar(300)
                            NOT NULL,

                        NotificationType nvarchar(50)
                            NOT NULL,

                        RelatedEntityType nvarchar(50)
                            NULL,

                        RelatedEntityId nvarchar(100)
                            NULL,

                        Status nvarchar(20)
                            NOT NULL
                            CONSTRAINT DF_EmailNotifications_Status
                            DEFAULT (N'Pending'),

                        AttemptCount int
                            NOT NULL
                            CONSTRAINT DF_EmailNotifications_AttemptCount
                            DEFAULT (0),

                        ErrorMessage nvarchar(2000)
                            NULL,

                        CreatedOn datetime2
                            NOT NULL,

                        SentOn datetime2
                            NULL,

                        CONSTRAINT PK_EmailNotifications
                            PRIMARY KEY
                            (
                                EmailNotificationId
                            )
                    );
                END;
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(
                    N'dbo.EmailNotifications',
                    N'U'
                ) IS NOT NULL
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name =
                        N'IX_EmailNotifications_Status_CreatedOn'
                      AND object_id =
                        OBJECT_ID(
                            N'dbo.EmailNotifications'
                        )
                )
                BEGIN
                    CREATE INDEX
                        IX_EmailNotifications_Status_CreatedOn
                    ON dbo.EmailNotifications
                    (
                        Status,
                        CreatedOn
                    );
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(
                    N'dbo.EmailNotifications',
                    N'U'
                ) IS NOT NULL
                BEGIN
                    DROP TABLE dbo.EmailNotifications;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'dbo.AspNetUsers',
                    'InvitationEmailError'
                ) IS NOT NULL
                BEGIN
                    ALTER TABLE dbo.AspNetUsers
                    DROP COLUMN InvitationEmailError;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'dbo.AspNetUsers',
                    'InvitationEmailSentOn'
                ) IS NOT NULL
                BEGIN
                    ALTER TABLE dbo.AspNetUsers
                    DROP COLUMN InvitationEmailSentOn;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'dbo.AspNetUsers',
                    'InvitationEmailStatus'
                ) IS NOT NULL
                BEGIN
                    DECLARE @InvitationStatusConstraint sysname;

                    SELECT
                        @InvitationStatusConstraint = dc.name
                    FROM sys.default_constraints AS dc
                    INNER JOIN sys.columns AS c
                        ON c.default_object_id = dc.object_id
                    WHERE dc.parent_object_id =
                        OBJECT_ID(N'dbo.AspNetUsers')
                      AND c.name =
                        N'InvitationEmailStatus';

                    IF @InvitationStatusConstraint IS NOT NULL
                    BEGIN
                        EXEC
                        (
                            N'ALTER TABLE dbo.AspNetUsers ' +
                            N'DROP CONSTRAINT [' +
                            @InvitationStatusConstraint +
                            N'];'
                        );
                    END;

                    ALTER TABLE dbo.AspNetUsers
                    DROP COLUMN InvitationEmailStatus;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'dbo.AspNetUsers',
                    'PasswordChangedOn'
                ) IS NOT NULL
                BEGIN
                    ALTER TABLE dbo.AspNetUsers
                    DROP COLUMN PasswordChangedOn;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'dbo.AspNetUsers',
                    'TemporaryPasswordIssuedOn'
                ) IS NOT NULL
                BEGIN
                    ALTER TABLE dbo.AspNetUsers
                    DROP COLUMN TemporaryPasswordIssuedOn;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH(
                    'dbo.AspNetUsers',
                    'MustChangePassword'
                ) IS NOT NULL
                BEGIN
                    DECLARE @MustChangePasswordConstraint sysname;

                    SELECT
                        @MustChangePasswordConstraint = dc.name
                    FROM sys.default_constraints AS dc
                    INNER JOIN sys.columns AS c
                        ON c.default_object_id = dc.object_id
                    WHERE dc.parent_object_id =
                        OBJECT_ID(N'dbo.AspNetUsers')
                      AND c.name =
                        N'MustChangePassword';

                    IF @MustChangePasswordConstraint IS NOT NULL
                    BEGIN
                        EXEC
                        (
                            N'ALTER TABLE dbo.AspNetUsers ' +
                            N'DROP CONSTRAINT [' +
                            @MustChangePasswordConstraint +
                            N'];'
                        );
                    END;

                    ALTER TABLE dbo.AspNetUsers
                    DROP COLUMN MustChangePassword;
                END;
                """);
        }
    }
}
