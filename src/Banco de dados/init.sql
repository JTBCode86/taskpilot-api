SET XACT_ABORT ON;
BEGIN TRANSACTION;
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
