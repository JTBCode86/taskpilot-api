ALTER TABLE dbo.Users
ADD EmailVerified BIT NOT NULL
        CONSTRAINT DF_Users_EmailVerified DEFAULT (0),
    EmailVerificationTokenHash NVARCHAR(64) NULL,
    EmailVerificationExpiresAtUtc DATETIMEOFFSET(7) NULL,
    EmailVerificationLastSentAtUtc DATETIMEOFFSET(7) NULL;

EXEC(N'UPDATE dbo.Users SET EmailVerified = 1;');

EXEC(N'SET QUOTED_IDENTIFIER ON;
CREATE UNIQUE INDEX UX_Users_EmailVerificationTokenHash
ON dbo.Users (EmailVerificationTokenHash)
WHERE EmailVerificationTokenHash IS NOT NULL;');
