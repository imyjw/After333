param(
    [string]$PublishedServerPath = "C:\Project_333\Builds\Server\Project333.PvpServer",
    [string]$ListenUrl = "http://0.0.0.0:7333",
    [string]$PublicServerUrl = "",
    [Parameter(Mandatory = $true)]
    [string]$DatabaseConnection,
    [Parameter(Mandatory = $true)]
    [string]$RequiredClientVersion,
    [string]$RecommendedClientVersion = "",
    [string]$CardDefinitionVersion = "",
    [int]$TicketPurchaseGoldCost = 3,
    [int]$RateLimitWindowSeconds = 60,
    [int]$AuthRateLimitPerWindow = 12,
    [int]$AccountRateLimitPerWindow = 120,
    [int]$BattleConnectRateLimitPerWindow = 20,
    [string]$GoogleClientIds = $env:PROJECT333_GOOGLE_CLIENT_IDS,
    [string]$GoogleDesktopClientId = $env:PROJECT333_GOOGLE_DESKTOP_CLIENT_ID,
    [string]$GoogleDesktopClientSecret = $env:PROJECT333_GOOGLE_DESKTOP_CLIENT_SECRET,
    [string]$LogDirectory = "C:\Project_333\Logs\Server",
    [int]$LogRetentionDays = 30,
    [int]$RestartDelaySeconds = 3,
    [int]$MaxRestartCount = 0,
    [switch]$DisableStartupRecovery,
    [switch]$VerboseTransportLogs,
    [switch]$RestartOnCleanExit,
    [switch]$PrintOnly
)

$ErrorActionPreference = "Stop"

function Set-Project333Env {
    param(
        [string]$Name,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        Remove-Item -Path "Env:$Name" -ErrorAction SilentlyContinue
        return
    }

    Set-Item -Path "Env:$Name" -Value $Value
}

function Format-MaskedConnectionString {
    param([string]$ConnectionString)

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        return "<not configured>"
    }

    return ($ConnectionString -replace '(?i)(Password|Pwd)=([^;]*)', '$1=****')
}

function Get-Project333Timestamp {
    return (Get-Date).ToString("yyyyMMdd_HHmmss")
}

function Remove-ExpiredProject333ServerLogs {
    param(
        [string]$Directory,
        [int]$RetentionDays
    )

    if ($RetentionDays -le 0) {
        Write-Host "[After333] Log retention disabled. Existing watchdog logs will be kept."
        return
    }

    if (-not (Test-Path -LiteralPath $Directory)) {
        return
    }

    $cutoffUtc = [DateTime]::UtcNow.AddDays(-$RetentionDays)
    $removedCount = 0
    Get-ChildItem -LiteralPath $Directory -File -ErrorAction SilentlyContinue |
        Where-Object {
            ($_.Name -like "server_*.out.log" -or $_.Name -like "server_*.err.log") -and
            $_.LastWriteTimeUtc -lt $cutoffUtc
        } |
        ForEach-Object {
            try {
                Remove-Item -LiteralPath $_.FullName -Force
                $removedCount++
            }
            catch {
                Write-Warning "[After333] Failed to remove expired log '$($_.FullName)': $($_.Exception.Message)"
            }
        }

    if ($removedCount -gt 0) {
        Write-Host "[After333] Removed $removedCount expired watchdog log file(s). Retention=${RetentionDays}d."
    }
}

function Write-Project333PublishManifestSummary {
    param([string]$PublishedServerPath)

    $manifestPath = Join-Path $PublishedServerPath "project333_server_publish_manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        Write-Host "  BuildManifest:  <missing>"
        return
    }

    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        Write-Host "  BuildManifest:  $manifestPath"
        Write-Host "  Build:          created=$($manifest.CreatedAtUtc) client=$($manifest.ClientVersion) assembly=$($manifest.ServerAssemblyVersion)"
        Write-Host "  Source:         branch=$($manifest.Git.Branch) commit=$($manifest.Git.Commit) dirty=$($manifest.Git.HasUncommittedChanges)"
    }
    catch {
        Write-Host "  BuildManifest:  $manifestPath (could not read: $($_.Exception.Message))"
    }
}

$serverDll = Join-Path $PublishedServerPath "Project333.PvpServer.dll"
if (-not (Test-Path -LiteralPath $serverDll)) {
    throw "Published server DLL was not found: $serverDll. Run PublishServer_Release.ps1 first."
}

if ([string]::IsNullOrWhiteSpace($DatabaseConnection)) {
    throw "RunPublishedServer_Watchdog.ps1 requires -DatabaseConnection. Production-like PvP must not run without PostgreSQL."
}

if ([string]::IsNullOrWhiteSpace($RequiredClientVersion)) {
    throw "RunPublishedServer_Watchdog.ps1 requires -RequiredClientVersion so clients can see the expected build version."
}

if ($RateLimitWindowSeconds -lt 1 -or
    $AuthRateLimitPerWindow -lt 1 -or
    $AccountRateLimitPerWindow -lt 1 -or
    $BattleConnectRateLimitPerWindow -lt 1) {
    throw "Rate-limit window and permit values must all be at least 1."
}

if ($RestartDelaySeconds -lt 0) {
    throw "RestartDelaySeconds must be 0 or greater."
}

if ($MaxRestartCount -lt 0) {
    throw "MaxRestartCount must be 0 or greater. Use 0 for unlimited restarts."
}

if ($LogRetentionDays -lt 0) {
    throw "LogRetentionDays must be 0 or greater. Use 0 to keep watchdog logs forever."
}

Set-Project333Env "ASPNETCORE_ENVIRONMENT" "Production"
Set-Project333Env "PROJECT333_PVP_SERVER_URL" $ListenUrl
Set-Project333Env "PROJECT333_PUBLIC_SERVER_URL" $PublicServerUrl
Set-Project333Env "PROJECT333_DB_CONNECTION" $DatabaseConnection
Set-Project333Env "PROJECT333_REQUIRED_CLIENT_VERSION" $RequiredClientVersion
Set-Project333Env "PROJECT333_RECOMMENDED_CLIENT_VERSION" $RecommendedClientVersion
Set-Project333Env "PROJECT333_CARD_DEFINITION_VERSION" $CardDefinitionVersion
Set-Project333Env "PROJECT333_GOOGLE_CLIENT_IDS" $GoogleClientIds
Set-Project333Env "PROJECT333_GOOGLE_DESKTOP_CLIENT_ID" $GoogleDesktopClientId
Set-Project333Env "PROJECT333_GOOGLE_DESKTOP_CLIENT_SECRET" $GoogleDesktopClientSecret
Set-Project333Env "PROJECT333_DEV_MIN_TICKETS" ""
Set-Project333Env "PROJECT333_TICKET_PURCHASE_GOLD_COST" "$TicketPurchaseGoldCost"
Set-Project333Env "PROJECT333_ENABLE_PVP_STARTUP_RECOVERY" $(if ($DisableStartupRecovery) { "0" } else { "1" })
Set-Project333Env "PROJECT333_VERBOSE_TRANSPORT_LOGS" $(if ($VerboseTransportLogs) { "1" } else { "0" })
Set-Project333Env "PROJECT333_RATE_LIMIT_ENABLED" "1"
Set-Project333Env "PROJECT333_RATE_LIMIT_WINDOW_SECONDS" "$RateLimitWindowSeconds"
Set-Project333Env "PROJECT333_AUTH_RATE_LIMIT_PER_WINDOW" "$AuthRateLimitPerWindow"
Set-Project333Env "PROJECT333_ACCOUNT_RATE_LIMIT_PER_WINDOW" "$AccountRateLimitPerWindow"
Set-Project333Env "PROJECT333_BATTLE_CONNECT_RATE_LIMIT_PER_WINDOW" "$BattleConnectRateLimitPerWindow"

Write-Host "[After333] Published server watchdog settings"
Write-Host "  Server DLL:      $serverDll"
Write-Project333PublishManifestSummary $PublishedServerPath
Write-Host "  ListenUrl:       $env:PROJECT333_PVP_SERVER_URL"
Write-Host "  PublicUrl:       $(if ([string]::IsNullOrWhiteSpace($env:PROJECT333_PUBLIC_SERVER_URL)) { '<same as ListenUrl>' } else { $env:PROJECT333_PUBLIC_SERVER_URL })"
Write-Host "  Database:        $(Format-MaskedConnectionString $env:PROJECT333_DB_CONNECTION)"
Write-Host "  Client:          required=$env:PROJECT333_REQUIRED_CLIENT_VERSION recommended=$env:PROJECT333_RECOMMENDED_CLIENT_VERSION cardDb=$env:PROJECT333_CARD_DEFINITION_VERSION"
Write-Host "  Tickets:         devMinimum=<disabled> purchaseGoldCost=$env:PROJECT333_TICKET_PURCHASE_GOLD_COST"
Write-Host "  Recovery:        startup=$env:PROJECT333_ENABLE_PVP_STARTUP_RECOVERY verboseTransport=$env:PROJECT333_VERBOSE_TRANSPORT_LOGS"
Write-Host "  RateLimits:      enabled=$env:PROJECT333_RATE_LIMIT_ENABLED window=${env:PROJECT333_RATE_LIMIT_WINDOW_SECONDS}s auth=$env:PROJECT333_AUTH_RATE_LIMIT_PER_WINDOW account=$env:PROJECT333_ACCOUNT_RATE_LIMIT_PER_WINDOW battleConnect=$env:PROJECT333_BATTLE_CONNECT_RATE_LIMIT_PER_WINDOW"
Write-Host "  Environment:     $env:ASPNETCORE_ENVIRONMENT"
Write-Host "  Logs:            $LogDirectory retention=$(if ($LogRetentionDays -eq 0) { 'forever' } else { "${LogRetentionDays}d" })"
Write-Host "  Restart:         delay=${RestartDelaySeconds}s max=$(if ($MaxRestartCount -eq 0) { 'unlimited' } else { $MaxRestartCount }) restartOnCleanExit=$RestartOnCleanExit"

if ($PrintOnly) {
    Write-Host "[After333] PrintOnly was set. Watchdog was not started."
    return
}

New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
Remove-ExpiredProject333ServerLogs $LogDirectory $LogRetentionDays

$restartCount = 0
while ($true) {
    $timestamp = Get-Project333Timestamp
    $stdoutPath = Join-Path $LogDirectory "server_$timestamp.out.log"
    $stderrPath = Join-Path $LogDirectory "server_$timestamp.err.log"

    Write-Host "[After333] Starting published server. stdout=$stdoutPath stderr=$stderrPath"
    $process = Start-Process -FilePath "dotnet" `
        -ArgumentList @("`"$serverDll`"") `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -NoNewWindow `
        -PassThru

    try {
        $process.WaitForExit()
    }
    finally {
        if (-not $process.HasExited) {
            Write-Host "[After333] Stopping child server process $($process.Id)."
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }

    $exitCode = $process.ExitCode
    Write-Host "[After333] Server process exited with code $exitCode."

    if ($exitCode -eq 0 -and -not $RestartOnCleanExit) {
        Write-Host "[After333] Clean exit detected. Watchdog is stopping."
        break
    }

    $restartCount++
    if ($MaxRestartCount -gt 0 -and $restartCount -gt $MaxRestartCount) {
        Write-Host "[After333] Max restart count reached. Watchdog is stopping."
        break
    }

    Write-Host "[After333] Restarting in $RestartDelaySeconds second(s). Restart count: $restartCount."
    Remove-ExpiredProject333ServerLogs $LogDirectory $LogRetentionDays
    if ($RestartDelaySeconds -gt 0) {
        Start-Sleep -Seconds $RestartDelaySeconds
    }
}
