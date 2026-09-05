param(
    [string]$DatabaseConnection = $env:PROJECT333_DB_CONNECTION,
    [string]$PostgresBinPath = "C:\Program Files\PostgreSQL\18\bin",
    [string]$BackupDirectory = "C:\Project_333\Backups\PostgreSQL",
    [string]$BackupName = "",
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

if ([string]::IsNullOrWhiteSpace($DatabaseConnection)) {
    throw "BackupPostgres_Project333.ps1 requires -DatabaseConnection or PROJECT333_DB_CONNECTION."
}

$connection = ConvertFrom-Project333ConnectionString $DatabaseConnection
if ([string]::IsNullOrWhiteSpace($connection.Database)) {
    throw "DatabaseConnection is missing Database=..."
}

if ([string]::IsNullOrWhiteSpace($connection.Username)) {
    throw "DatabaseConnection is missing Username=..."
}

$pgDump = Resolve-Project333PostgresTool "pg_dump.exe" $PostgresBinPath
$timestamp = (Get-Date).ToString("yyyyMMdd_HHmmss")
if ([string]::IsNullOrWhiteSpace($BackupName)) {
    $BackupName = "project333_$timestamp.dump"
}

if (-not $BackupName.EndsWith(".dump", [System.StringComparison]::OrdinalIgnoreCase)) {
    $BackupName = "$BackupName.dump"
}

$backupPath = Join-Path $BackupDirectory $BackupName

Write-Host "[After333] PostgreSQL backup settings"
Write-Host "  Tool:       $pgDump"
Write-Host "  Database:   $($connection.Database) on $($connection.Host):$($connection.Port)"
Write-Host "  User:       $($connection.Username)"
Write-Host "  Connection: $(Format-MaskedConnectionString $DatabaseConnection)"
Write-Host "  Output:     $backupPath"

if ($PrintOnly) {
    Write-Host "[After333] PrintOnly was set. Backup was not started."
    return
}

New-Item -ItemType Directory -Force -Path $BackupDirectory | Out-Null

$previousPassword = $env:PGPASSWORD
try {
    $env:PGPASSWORD = $connection.Password
    & $pgDump `
        -h $connection.Host `
        -p $connection.Port `
        -U $connection.Username `
        -d $connection.Database `
        -F c `
        -f $backupPath

    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump failed with exit code $LASTEXITCODE."
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

Write-Host "[After333] Backup complete: $backupPath"
