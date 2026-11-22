-- Usuario de exemplo somente para desenvolvimento; nao possui senha utilizavel.
INSERT INTO Users (FirstName, LastName, Email, PasswordHash, IsActive, CreatedAt)
VALUES (N'FirstUser', N'Test', N'teste@taskpilot.com', N'not-a-valid-password-hash', 1, GETDATE());
GO
