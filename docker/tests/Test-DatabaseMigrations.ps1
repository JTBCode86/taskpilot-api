$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$suffix = [Guid]::NewGuid().ToString('N')
$project = "taskpilot-migration-test-$suffix"
$fixture = Join-Path ([System.IO.Path]::GetTempPath()) "taskpilot-migration-test-$suffix"
$composeFile = Join-Path $repo 'compose.yaml'
$originalPassword = $env:MSSQL_SA_PASSWORD
$originalPort = $env:TASKPILOT_API_PORT
$env:MSSQL_SA_PASSWORD = 'LocalMigrationTest_123'
$env:TASKPILOT_API_PORT = '0'
$containers = @()
$fixtureCreated = $false
$projectStarted = $false

function Invoke-Compose {
    & docker compose -p $project -f $composeFile @args
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Initializer([bool] $ShouldFail = $false) {
    & docker compose -p $project -f $composeFile run --rm --no-deps -v "${fixture}:/scripts:ro" db-init
    if (($LASTEXITCODE -eq 0) -eq $ShouldFail) {
        throw "Unexpected initializer exit code $LASTEXITCODE."
    }
}

function Assert-Sql([string] $query) {
    & docker compose -p $project -f $composeFile exec -T -e "SQLCMDPASSWORD=$env:MSSQL_SA_PASSWORD" db `
        /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d TaskPilotDB -Q $query
    if ($LASTEXITCODE -ne 0) {
        throw "SQL assertion failed with exit code $LASTEXITCODE."
    }
}

function Write-Migration([string] $name, [string] $sql) {
    $path = Join-Path (Join-Path $fixture 'Migrations') $name
    [System.IO.File]::WriteAllText($path, $sql + "`n", [System.Text.UTF8Encoding]::new($false))
}

try {
    if (Test-Path -LiteralPath $fixture) {
        throw "Temporary test path already exists: $fixture"
    }
    $existingContainers = & docker ps -aq --filter "label=com.docker.compose.project=$project"
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect Docker containers.' }
    $existingVolume = & docker volume ls -q --filter "name=^${project}_sql_data$"
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect Docker volumes.' }
    if ($existingContainers -or $existingVolume) {
        throw "Temporary Docker project already exists: $project"
    }
    $fixtureCreated = $true
    Copy-Item -LiteralPath (Join-Path $repo 'src\Banco de dados') -Destination $fixture -Recurse
    $metadata = Join-Path $fixture 'Migrations\0002_migration_metadata.sql'
    $metadataContent = [System.IO.File]::ReadAllText($metadata)
    Remove-Item -LiteralPath $metadata

    $projectStarted = $true
    Invoke-Compose up -d db

    # Create the same version-1 baseline as an existing volume, then upgrade it.
    Invoke-Initializer $true
    Assert-Sql "IF NOT EXISTS (SELECT 1 FROM dbo.TaskPilotSchemaVersion WHERE VersionNumber = 1) THROW 52000, 'Baseline missing', 1;"
    Assert-Sql "INSERT INTO dbo.Tasks (UserId, StatusId, Title, PriorityLevel) VALUES (1, 1, N'legacy task', 1);"
    [System.IO.File]::WriteAllText($metadata, $metadataContent, [System.Text.UTF8Encoding]::new($false))
    Write-Migration '0003_test.sql' "CREATE TABLE dbo.MigrationTest (Id INT NOT NULL PRIMARY KEY);`nINSERT INTO dbo.MigrationTest (Id) VALUES (1);"
    Invoke-Initializer
    Invoke-Initializer
    Assert-Sql "IF (SELECT COUNT(*) FROM dbo.TaskPilotSchemaVersion) <> 3 OR (SELECT COUNT(*) FROM dbo.MigrationTest) <> 1 OR NOT EXISTS (SELECT 1 FROM dbo.Tasks WHERE Title = N'legacy task') THROW 52001, 'Upgrade or repeat failed', 1;"
    Assert-Sql "IF EXISTS (SELECT 1 FROM dbo.TaskPilotSchemaVersion WHERE VersionNumber = 1 AND (ScriptName IS NOT NULL OR Checksum IS NOT NULL OR AppliedAtUtc IS NOT NULL)) OR EXISTS (SELECT 1 FROM dbo.TaskPilotSchemaVersion WHERE VersionNumber > 1 AND (ScriptName IS NULL OR Checksum IS NULL OR AppliedAtUtc IS NULL)) THROW 52010, 'Migration metadata is invalid', 1;"

    Write-Migration '0004_test.sql' "INSERT INTO dbo.MigrationTest (Id) VALUES (2);`nTHROW 52002, 'Intentional test failure', 1;"
    Invoke-Initializer $true
    Assert-Sql "IF EXISTS (SELECT 1 FROM dbo.MigrationTest WHERE Id = 2) OR EXISTS (SELECT 1 FROM dbo.TaskPilotSchemaVersion WHERE VersionNumber = 4) THROW 52003, 'Failed migration was not rolled back', 1;"

    Write-Migration '0004_test.sql' "WAITFOR DELAY '00:00:02';`nINSERT INTO dbo.MigrationTest (Id) VALUES (2);"
    for ($i = 0; $i -lt 2; $i++) {
        $id = & docker compose -p $project -f $composeFile run -d --no-deps -v "${fixture}:/scripts:ro" db-init
        if ($LASTEXITCODE -ne 0 -or -not $id) {
            throw 'Could not start concurrent initializer.'
        }
        $containers += $id.Trim()
    }
    foreach ($id in $containers) {
        $exitCode = & docker wait $id
        if ($LASTEXITCODE -ne 0 -or $exitCode.Trim() -ne '0') {
            & docker logs $id
            throw 'Concurrent initialization failed.'
        }
    }
    Assert-Sql "IF (SELECT COUNT(*) FROM dbo.MigrationTest) <> 2 OR (SELECT COUNT(*) FROM dbo.TaskPilotSchemaVersion WHERE VersionNumber = 4) <> 1 THROW 52004, 'Concurrent migration failed', 1;"

    Write-Migration '0003_test.sql' "SELECT 1;"
    Invoke-Initializer $true
    Assert-Sql "IF (SELECT COUNT(*) FROM dbo.MigrationTest) <> 2 THROW 52005, 'History validation changed data', 1;"

    Remove-Item -LiteralPath (Join-Path $fixture 'Migrations\0003_test.sql')
    Invoke-Initializer $true
    Assert-Sql "IF (SELECT COUNT(*) FROM dbo.MigrationTest) <> 2 THROW 52008, 'Missing migration changed data', 1;"
    Write-Migration '0003_test.sql' "CREATE TABLE dbo.MigrationTest (Id INT NOT NULL PRIMARY KEY);`nINSERT INTO dbo.MigrationTest (Id) VALUES (1);"
    Write-Migration '0005_test.sql' "SELECT 1;`nGO"
    Invoke-Initializer $true
    Assert-Sql "IF EXISTS (SELECT 1 FROM dbo.TaskPilotSchemaVersion WHERE VersionNumber = 5) THROW 52006, 'Unsupported batch was applied', 1;"

    Write-Migration '0005_test.sql' "INSERT INTO dbo.MigrationTest (Id) VALUES (3);"
    Assert-Sql "INSERT INTO dbo.TaskPilotSchemaVersion (VersionNumber) VALUES (6);"
    Invoke-Initializer $true
    Assert-Sql "IF EXISTS (SELECT 1 FROM dbo.MigrationTest WHERE Id = 3) OR EXISTS (SELECT 1 FROM dbo.TaskPilotSchemaVersion WHERE VersionNumber = 5) THROW 52007, 'Unknown history allowed migration', 1;"

    Assert-Sql "EXEC sys.sp_rename N'dbo.TaskPilotSchemaVersion', N'UnmanagedVersion';"
    Invoke-Initializer $true
    Assert-Sql "IF NOT EXISTS (SELECT 1 FROM dbo.Tasks WHERE Title = N'legacy task') OR OBJECT_ID(N'dbo.TaskPilotSchemaVersion', N'U') IS NOT NULL THROW 52009, 'Unmanaged database was modified', 1;"
    Write-Output 'Migration integration tests passed.'
}
finally {
    $cleanupFailed = $false
    foreach ($id in $containers) {
        & docker rm -f $id
        if ($LASTEXITCODE -ne 0) { $cleanupFailed = $true }
    }
    if ($projectStarted) {
        & docker compose -p $project -f $composeFile down --volumes --remove-orphans
        if ($LASTEXITCODE -ne 0) { $cleanupFailed = $true }
    }
    if ($fixtureCreated -and (Test-Path -LiteralPath $fixture)) {
        Remove-Item -LiteralPath $fixture -Recurse -Force
    }
    $env:MSSQL_SA_PASSWORD = $originalPassword
    $env:TASKPILOT_API_PORT = $originalPort
    if ($cleanupFailed) { throw "Failed to clean up temporary Docker project $project." }
}
