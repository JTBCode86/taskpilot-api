SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @LockResult INT;
EXEC @LockResult = sys.sp_getapplock
    @Resource = N'TaskPilotSchemaMigration',
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 60000;
IF @LockResult < 0
    THROW 51000, 'Could not acquire the schema migration lock.', 1;
IF OBJECT_ID(N'dbo.TaskPilotSchemaVersion', N'U') IS NOT NULL
    THROW 51001, 'TaskPilotDB was initialized by another process; retry initialization.', 1;
GO
:r "/scripts/Scripts DDL/Tabelas/01_DDL_Users.sql"
:r "/scripts/Scripts DDL/Tabelas/02_DDL_TaskStatuses.sql"
:r "/scripts/Scripts DDL/Tabelas/03_DDL_TaskCategories.sql"
:r "/scripts/Scripts DDL/Tabelas/04_DDL_Tasks.sql"
:r "/scripts/Scripts DDL/Tabelas/05_DDL_TaskCategoryMap.sql"
:r "/scripts/Scripts DML/01_DML_TaskStatuses.sql"
:r "/scripts/Scripts DML/02_DML_Users.sql"
:r "/scripts/Scripts DDL/Procedures/dbo.SpCreateNewTask.sql"
:r "/scripts/Scripts DDL/Procedures/dbo.SpMarkTaskAsCompleted.sql"
:r "/scripts/Scripts DDL/Procedures/dbo.SpGetOverdueTasksByUserId.sql"
:r "/scripts/Scripts DDL/Views/dbo.vw_UserTaskSummary.sql"
:r "/scripts/Scripts DDL/Views/dbo.vw_ProductivityAnalysisData.sql"
CREATE TABLE dbo.TaskPilotSchemaVersion (VersionNumber INT NOT NULL PRIMARY KEY);
INSERT INTO dbo.TaskPilotSchemaVersion (VersionNumber) VALUES (1);
COMMIT TRANSACTION;
GO
