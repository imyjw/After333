param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,
    [string]$DatabaseConnection = $env:PROJECT333_DB_CONNECTION,
    [string]$PostgresBinPath = "C:\Program Files\PostgreSQL\18\bin",
    [switch]$ConfirmRestore,
    [switch]$TerminateExistingConnections,
    [switch]$PrintOnly
)

$ErrorActionPreference = "Stop"

function ConvertFrom-Project333ConnectionString {
    param([string]$ConnectionString)

    $settings = @{}
    foreach ($part in $ConnectionString -split ';') {
        if ([string]::IsNullOrWhiteSpace($part)) {
            continue
        }

        $index = $part.IndexOf('=')
        if ($index -lt 1) {
            continue
        }

        $key = $part.Substring(0, $index).Trim().ToLowerInvariant()
        $value = $part.Substring($index + 1).Trim()
        $settings[$key] = $value
    }

    return [pscustomobject]@{
        Host = if ($settings.ContainsKey("host")) { $settings["host"] } elseif ($settings.ContainsKey("server")) { $settings["server"] } else { "127.0.0.1" }
        Port = if ($settings.ContainsKey("port")) { $settings["port"] } else { "5432" }
        Database = if ($settings.ContainsKey("database")) { $settings["database"] } elseif ($settings.ContainsKey("dbname")) { $settings["dbname"] } else { "" }
        Username = if ($settings.ContainsKey("username")) { $settings["username"] } elseif ($settings.ContainsKey("user id")) { $settings["user id"] } elseif ($settings.ContainsKey("user")) { $settings["user"] } else { "" }
        Password = if ($settings.ContainsKey("password")) { $settings["password"] } elseif ($settings.ContainsKey("pwd")) { $settings["pwd"] } else { "" }
    }
}

function Resolve-Project333PostgresTool {
    param(
        [string]$ToolName,
        [string]$BinPath
    )

    if (-not [string]::IsNullOrWhiteSpace($BinPath)) {
        $candidate = Join-Path $BinPath $ToolName
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $command = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($command -ne $null) {
        return $command.Source
    }

    throw "$ToolName was not found. Pass -PostgresBinPath, for example: C:\Program Files\PostgreSQL\18\bin"
}

function Format-MaskedConnectionString {
    param([string]$ConnectionString)

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        return "<not configured>"
    }

    return ($ConnectionString -replace '(?i)(Password|Pwd)=([^;]*)', '$1=****')
}

if (-not (Test-Path -LiteralPath $BackupPath)) {
    throw "Backup file was not found: $BackupPath"
}

if ([string]::IsNullOrWhiteSpace($DatabaseConnection)) {
    throw "RestorePostgres_Project333.ps1 requires -DatabaseConnection or PROJECT333_DB_CONNECTION."
}

$connection = ConvertFrom-Project333ConnectionString $DatabaseConnection
if ([string]::IsNullOrWhiteSpace($connection.Database)) {
    throw "DatabaseConnection is missing Database=..."
}

if ([string]::IsNullOrWhiteSpace($connection.Username)) {
    throw "DatabaseConnection is missing Username=..."
}

$pgRestore = Resolve-Project333PostgresTool "pg_restore.exe" $PostgresBinPath
$psql = $null
if ($TerminateExistingConnections) {
    $psql = Resolve-Project333PostgresTool "psql.exe" $PostgresBinPath
}

Write-Host "[After333] PostgreSQL restore settings"
Write-Host "  Restore Tool: $pgRestore"
if ($TerminateExistingConnections) {
    Write-Host "  PSQL Tool:    $psql"
}
Write-Host "  Database:     $($connection.Database) on $($connection.Host):$($connection.Port)"
Write-Host "  User:         $($connection.Username)"
Write-Host "  Connection:   $(Format-MaskedConnectionString $DatabaseConnection)"
Write-Host "  Backup:       $BackupPath"
Write-Host "  Terminate existing connections: $TerminateExistingConnections"

if ($PrintOnly) {
    Write-Host "[After333] PrintOnly was set. Restore was not started."
    return
}

if (-not $ConfirmRestore) {
    throw "Refusing to restore without -ConfirmRestore. Stop the server, verify the backup path, then rerun with -ConfirmRestore."
}

$previousPassword = $env:PGPASSWORD
try {
    $env:PGPASSWORD = $connection.Password

    if ($TerminateExistingConnections) {
        $sql = "select pg_terminate_backend(pid) from pg_stat_activity where datname = '$($connection.Database.Replace("'", "''"))' and pid <> pg_backend_pid();"
        & $psql `
            -h $connection.Host `
            -p $connection.Port `
            -U $connection.Username `
            -d "postgres" `
            -c $sql

        if ($LASTEXITCODE -ne 0) {
            throw "psql connection termination failed with exit code $LASTEXITCODE."
        }
    }

    & $pgRestore `
        -h $connection.Host `
        -p $connection.Port `
        -U $connection.Username `
        -d $connection.Database `
        --clean `
        --if-exists `
        --no-owner `
        --no-privileges `
        $BackupPath

    if ($LASTEXITCODE -ne 0) {
        throw "pg_restore failed with exit code $LASTEXITCODE."
    }
}
finally {
    if ([string]::IsNullOrEmpty($previousPassword)) {
        Remove-Item -Path "Env:PGPASSWORD" -ErrorAction SilentlyContinue
    }
    else {
        $env:PGPASSWORD = $previousPassword
    }
}

Write-Host "[After333] Restore complete."
