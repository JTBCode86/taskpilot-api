SET NOCOUNT ON
GO
CREATE OR ALTER PROCEDURE SpGetOverdueTasksByUserId
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CompletedStatusId INT = 3; 

    SELECT T.*
    FROM Tasks T
    WHERE T.UserId = @UserId
      AND T.DueDate < GETDATE()
      AND T.StatusId != @CompletedStatusId
      AND T.CompletedAt IS NULL -- Garantia extra
    ORDER BY T.DueDate;
END
GO