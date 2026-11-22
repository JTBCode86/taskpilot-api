#!/bin/bash
set -euo pipefail

sqlcmd=/opt/mssql-tools18/bin/sqlcmd
for attempt in $(seq 1 60); do
    if "$sqlcmd" -S db -U sa -C -l 2 -b -Q "SELECT 1" > /dev/null 2>&1; then
        break
    fi
    if [ "$attempt" -eq 60 ]; then
        echo "SQL Server did not become ready; check the database logs and credentials." >&2
        exit 1
    fi
    sleep 2
done

"$sqlcmd" -S db -U sa -C -b -Q "IF DB_ID(N'TaskPilotDB') IS NULL EXEC(N'CREATE DATABASE TaskPilotDB')"

state=$("$sqlcmd" -S db -U sa -C -b -d TaskPilotDB -h -1 -W -Q \
    "SET NOCOUNT ON; IF OBJECT_ID(N'dbo.TaskPilotSchemaVersion', N'U') IS NOT NULL SELECT 'initialized'; ELSE IF EXISTS (SELECT 1 FROM sys.objects WHERE type IN ('U', 'P', 'V') AND is_ms_shipped = 0) SELECT 'existing'; ELSE SELECT 'empty';" | tr -d '[:space:]')

case "$state" in
    initialized)
        echo "TaskPilotDB already initialized; checking migrations."
        ;;
    empty)
        "$sqlcmd" -S db -U sa -C -b -f 65001 -d TaskPilotDB -i /scripts/init.sql
        echo "TaskPilotDB initialized."
        ;;
    *)
        echo "TaskPilotDB is not empty and has no initialization marker; refusing to modify it." >&2
        exit 1
        ;;
esac

export LC_ALL=C
shopt -s nullglob
migrations=(/scripts/Migrations/*.sql)
migration_batch=$(mktemp)
snapshots=()
names=()
checksums=()
cleanup() {
    rm -f -- "$migration_batch" "${snapshots[@]}"
}
trap cleanup EXIT
expected=2
known_versions=1
for migration in "${migrations[@]}"; do
    name=${migration##*/}
    if [[ ! "$name" =~ ^([0-9]{4})_([a-z0-9][a-z0-9_]*)\.sql$ ]]; then
        echo "Invalid migration filename: $name" >&2
        exit 1
    fi
    version=$((10#${BASH_REMATCH[1]}))
    if [ "$version" -ne "$expected" ]; then
        echo "Missing or duplicate migration version: expected $expected, found $name." >&2
        exit 1
    fi
    snapshot=$(mktemp)
    snapshots+=("$snapshot")
    cp -- "$migration" "$snapshot"
    if grep -Eq '^[[:space:]]*([Gg][Oo]([[:space:];]|$)|:)' "$snapshot"; then
        echo "Migration $name contains GO or a sqlcmd directive, which is not supported." >&2
        exit 1
    fi
    names+=("$name")
    checksums+=("$(sha256sum "$snapshot" | cut -d ' ' -f 1)")
    known_versions="$known_versions,$version"
    expected=$((expected + 1))
done
if [ "$expected" -eq 2 ]; then
    echo "Migration 0002_migration_metadata.sql is required." >&2
    exit 1
fi

cat > "$migration_batch" <<SQL
SET XACT_ABORT ON;
DECLARE @LockResult INT;
EXEC @LockResult = sys.sp_getapplock
    @Resource = N'TaskPilotSchemaMigration',
    @LockMode = 'Exclusive',
    @LockOwner = 'Session',
    @LockTimeout = 60000;
IF @LockResult < 0
    THROW 51000, 'Could not acquire the schema migration lock.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.TaskPilotSchemaVersion WHERE VersionNumber = 1)
    THROW 51002, 'The version 1 baseline is missing.', 1;
IF EXISTS (SELECT 1 FROM dbo.TaskPilotSchemaVersion WHERE VersionNumber NOT IN ($known_versions))
    THROW 51003, 'The database contains an unknown schema version.', 1;
GO
SQL

for index in "${!names[@]}"; do
    name=${names[$index]}
    version=$((10#${name:0:4}))
    checksum=${checksums[$index]}
    previous=$((version - 1))
    cat >> "$migration_batch" <<SQL
IF EXISTS (SELECT 1 FROM dbo.TaskPilotSchemaVersion WHERE VersionNumber = $version)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.TaskPilotSchemaVersion WHERE VersionNumber = $previous)
        THROW 51004, 'The schema migration history has a gap.', 1;
    IF COL_LENGTH(N'dbo.TaskPilotSchemaVersion', N'Checksum') IS NULL
        OR COL_LENGTH(N'dbo.TaskPilotSchemaVersion', N'ScriptName') IS NULL
        OR COL_LENGTH(N'dbo.TaskPilotSchemaVersion', N'AppliedAtUtc') IS NULL
        THROW 51005, 'The schema migration metadata is missing.', 1;
    EXEC sys.sp_executesql N'IF NOT EXISTS (
        SELECT 1 FROM dbo.TaskPilotSchemaVersion
        WHERE VersionNumber = $version AND ScriptName = N''$name'' AND Checksum = ''$checksum''
    ) THROW 51006, ''An applied migration was changed or renamed.'', 1;';
END;
GO
SQL
done

for index in "${!names[@]}"; do
    name=${names[$index]}
    version=$((10#${name:0:4}))
    checksum=${checksums[$index]}
    cat >> "$migration_batch" <<SQL
BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM dbo.TaskPilotSchemaVersion WHERE VersionNumber = $version)
BEGIN
    PRINT 'Applying migration $name';
:r "${snapshots[$index]}"
    EXEC sys.sp_executesql N'INSERT INTO dbo.TaskPilotSchemaVersion (VersionNumber, ScriptName, Checksum)
        VALUES ($version, N''$name'', ''$checksum'');';
END
ELSE
    PRINT 'Migration $name already applied';
COMMIT TRANSACTION;
GO
SQL
done

"$sqlcmd" -S db -U sa -C -b -x -f 65001 -d TaskPilotDB -i "$migration_batch"
echo "TaskPilotDB migrations are up to date."
