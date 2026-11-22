#!/bin/bash
set -eu

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
        echo "TaskPilotDB already initialized; preserving existing data."
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
