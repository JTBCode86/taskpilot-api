ALTER TABLE dbo.TaskPilotSchemaVersion
ADD ScriptName NVARCHAR(255) NULL,
    Checksum CHAR(64) NULL,
    AppliedAtUtc DATETIME2(7) NULL
        CONSTRAINT DF_TaskPilotSchemaVersion_AppliedAtUtc DEFAULT SYSUTCDATETIME();
