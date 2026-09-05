param(
    [string]$BaseUrl = "http://127.0.0.1:7333",
    [string]$BattleWebSocketUrl = "",
    [string]$ExpectedPublicUrl = "",
    [string]$ExpectedRequiredClientVersion = "",
    [int]$TimeoutSeconds = 8,
    [string]$ServerLogDirectory = "C:\Project_333\Logs\Server",
    [string]$AuditLogDirectory = "",
    [switch]$AllowDeploymentWarnings,
    [switch]$SkipWebSocket,
    [switch]$SkipLocalLogCheck,
    [switch]$PrintOnly
)

$ErrorActionPreference = "Stop"

function Normalize-HttpUrl {
    param([string]$Url)

    if ([string]::IsNullOrWhiteSpace($Url)) {
        return "http://127.0.0.1:7333"
    }

    return $Url.Trim().TrimEnd("/")
}

function Resolve-BattleWebSocketUrl {
    param(
        [string]$HttpUrl,
        [string]$ExplicitWebSocketUrl
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitWebSocketUrl)) {
        $webSocketUrl = $ExplicitWebSocketUrl.Trim().TrimEnd("/")
    }
    elseif ($HttpUrl.StartsWith("https://", [System.StringComparison]::OrdinalIgnoreCase)) {
        $webSocketUrl = "wss://" + $HttpUrl.Substring("https://".Length)
    }
    elseif ($HttpUrl.StartsWith("http://", [System.StringComparison]::OrdinalIgnoreCase)) {
        $webSocketUrl = "ws://" + $HttpUrl.Substring("http://".Length)
    }
    else {
        throw "BaseUrl must start with http:// or https:// when BattleWebSocketUrl is omitted."
    }

    if ($webSocketUrl.EndsWith("/battle", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $webSocketUrl
    }

    return "$webSocketUrl/battle"
}

function Invoke-JsonGet {
    param(
        [string]$Url,
        [int]$TimeoutSeconds
    )

    return Invoke-RestMethod `
        -Method Get `
        -Uri $Url `
        -TimeoutSec $TimeoutSeconds
}

function Add-SmokeFailure {
    param(
        [System.Collections.Generic.List[string]]$Failures,
        [string]$Message
    )

    $Failures.Add($Message) | Out-Null
    Write-Host "[public-smoke] FAIL: $Message"
}

function Add-SmokeWarning {
    param(
        [System.Collections.Generic.List[string]]$Warnings,
        [string]$Message
    )

    $Warnings.Add($Message) | Out-Null
    Write-Host "[public-smoke] warning: $Message"
}

function Test-Project333WebSocket {
    param(
        [string]$Url,
        [int]$TimeoutSeconds
    )

    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $cancellation = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))

    try {
        $null = $socket.ConnectAsync([System.Uri]$Url, $cancellation.Token).GetAwaiter().GetResult()
        if ($socket.State -ne [System.Net.WebSockets.WebSocketState]::Open) {
            throw "WebSocket did not open. State=$($socket.State)"
        }

        Write-Host "[public-smoke] websocket connected."
    }
    finally {
        if ($socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
            $closeCancellation = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))
            try {
                $null = $socket.CloseOutputAsync(
                    [System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,
                    "public pvp smoke readiness test complete",
                    $closeCancellation.Token).GetAwaiter().GetResult()
            }
            catch {
                Write-Host "[public-smoke] websocket close notice: $($_.Exception.Message)"
            }
            finally {
                $closeCancellation.Dispose()
            }
        }

        $cancellation.Dispose()
        $socket.Dispose()
    }
}

function Get-LatestProject333LogFile {
    param(
        [string]$Directory,
        [string]$Filter
    )

    if ([string]::IsNullOrWhiteSpace($Directory) -or -not (Test-Path -LiteralPath $Directory)) {
        return $null
    }

    return Get-ChildItem -LiteralPath $Directory -Filter $Filter -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

function Format-OptionalValue {
    param([object]$Value)

    if ($null -eq $Value) {
        return "-"
    }

    if ($Value -is [string] -and [string]::IsNullOrWhiteSpace($Value)) {
        return "-"
    }

    return "$Value"
}

$BaseUrl = Normalize-HttpUrl $BaseUrl
$BattleWebSocketUrl = Resolve-BattleWebSocketUrl $BaseUrl $BattleWebSocketUrl

Write-Host "[public-smoke] BaseUrl=$BaseUrl"
Write-Host "[public-smoke] BattleWebSocketUrl=$BattleWebSocketUrl"
Write-Host "[public-smoke] ExpectedPublicUrl=$(if ([string]::IsNullOrWhiteSpace($ExpectedPublicUrl)) { '-' } else { $ExpectedPublicUrl.Trim().TrimEnd('/') })"
Write-Host "[public-smoke] AllowDeploymentWarnings=$AllowDeploymentWarnings SkipWebSocket=$SkipWebSocket SkipLocalLogCheck=$SkipLocalLogCheck"

if ($PrintOnly) {
    Write-Host "[public-smoke] PrintOnly was set. No endpoint checks were started."
    return
}

$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

try {
    Write-Host "[public-smoke] GET /health"
    $health = Invoke-JsonGet "$BaseUrl/health" $TimeoutSeconds
    if ($health.Status -ne "Ok") {
        Add-SmokeFailure $failures "/health returned Status=$($health.Status), expected Ok."
    }
    else {
        Write-Host "[public-smoke] health ok. serverTime=$($health.ServerTimeUtc)"
    }
}
catch {
    Add-SmokeFailure $failures "/health request failed: $($_.Exception.Message)"
}

$serverStatus = $null
try {
    Write-Host "[public-smoke] GET /server/status"
    $serverStatus = Invoke-JsonGet "$BaseUrl/server/status" $TimeoutSeconds
    if ($serverStatus.Status -ne "Ok") {
        Add-SmokeFailure $failures "/server/status returned Status=$($serverStatus.Status), expected Ok."
    }
    else {
        Write-Host "[public-smoke] server status ok. version=$($serverStatus.Version) env=$($serverStatus.Environment)"
    }
}
catch {
    Add-SmokeFailure $failures "/server/status request failed: $($_.Exception.Message)"
}

if ($null -ne $serverStatus) {
    $actualPublicUrl = "$($serverStatus.PublicUrl)".Trim().TrimEnd('/')
    Write-Host "[public-smoke] server listen=$($serverStatus.ListenUrl) public=$actualPublicUrl"
    if (-not [string]::IsNullOrWhiteSpace($ExpectedPublicUrl) -and
        $actualPublicUrl -ne $ExpectedPublicUrl.Trim().TrimEnd('/')) {
        Add-SmokeFailure $failures "PublicUrl=$actualPublicUrl, expected $($ExpectedPublicUrl.Trim().TrimEnd('/'))."
    }

    if ($null -eq $serverStatus.Database) {
        Add-SmokeFailure $failures "Database block is missing from /server/status."
    }
    else {
        if ($serverStatus.Database.Status -ne "Ok") {
            Add-SmokeFailure $failures "Database.Status=$($serverStatus.Database.Status), expected Ok."
        }

        if ($serverStatus.Database.Configured -ne $true) {
            Add-SmokeFailure $failures "Database.Configured is not true."
        }

        if ($serverStatus.Database.Reachable -ne $true) {
            Add-SmokeFailure $failures "Database.Reachable is not true."
        }

        Write-Host "[public-smoke] database status=$($serverStatus.Database.Status) migrations=$($serverStatus.Database.AppliedMigrationCount) latest=$($serverStatus.Database.LatestMigration)"
    }

    if ($null -eq $serverStatus.Cards) {
        Add-SmokeFailure $failures "Cards block is missing from /server/status."
    }
    else {
        if ($serverStatus.Cards.Status -ne "Ok") {
            Add-SmokeFailure $failures "Cards.Status=$($serverStatus.Cards.Status), expected Ok."
        }

        if ($serverStatus.Cards.CardCount -le 0) {
            Add-SmokeFailure $failures "Cards.CardCount=$($serverStatus.Cards.CardCount), expected more than 0."
        }

        Write-Host "[public-smoke] cards status=$($serverStatus.Cards.Status) count=$($serverStatus.Cards.CardCount) schema=$($serverStatus.Cards.SchemaVersion)"
    }

    if ($null -eq $serverStatus.PvP) {
        Add-SmokeFailure $failures "PvP block is missing from /server/status."
    }
    else {
        if ($serverStatus.PvP.ReconnectGraceSeconds -ne 60) {
            Add-SmokeFailure $failures "PvP.ReconnectGraceSeconds=$($serverStatus.PvP.ReconnectGraceSeconds), expected 60."
        }

        Write-Host "[public-smoke] pvp reconnectGrace=$($serverStatus.PvP.ReconnectGraceSeconds) startupRecovery=$($serverStatus.PvP.StartupRecoveryEnabled) verboseTransport=$($serverStatus.PvP.VerboseTransportLogsEnabled)"
    }

    if ($null -eq $serverStatus.Audit) {
        Add-SmokeFailure $failures "Audit block is missing from /server/status. Restart the server with the current build."
    }
    else {
        if ($serverStatus.Audit.Enabled -ne $true) {
            Add-SmokeFailure $failures "Audit logging is disabled. Public PvP smoke tests should keep audit logs enabled."
        }

        if ([string]::IsNullOrWhiteSpace($AuditLogDirectory)) {
            $AuditLogDirectory = $serverStatus.Audit.LogDirectory
        }

        Write-Host "[public-smoke] audit enabled=$($serverStatus.Audit.Enabled) retentionDays=$($serverStatus.Audit.RetentionDays) dir=$($serverStatus.Audit.LogDirectory)"
    }

    if ($null -eq $serverStatus.Deployment) {
        Add-SmokeFailure $failures "Deployment block is missing from /server/status."
    }
    else {
        $deploymentWarnings = @($serverStatus.Deployment.Warnings)
        foreach ($warning in $deploymentWarnings) {
            Add-SmokeWarning $warnings "deployment warning: $warning"
        }

        if ($deploymentWarnings.Count -gt 0 -and -not $AllowDeploymentWarnings) {
            Add-SmokeFailure $failures "Deployment has $($deploymentWarnings.Count) warning(s). Use -AllowDeploymentWarnings only for local/LAN tests."
        }

        Write-Host "[public-smoke] deployment status=$($serverStatus.Deployment.Status) mode=$($serverStatus.Deployment.Mode) ready=$($serverStatus.Deployment.Ready)"
    }

    if ($null -eq $serverStatus.ClientCompatibility) {
        Add-SmokeFailure $failures "ClientCompatibility block is missing from /server/status."
    }
    else {
        $requiredClientVersion = Format-OptionalValue $serverStatus.ClientCompatibility.RequiredClientVersion
        $recommendedClientVersion = Format-OptionalValue $serverStatus.ClientCompatibility.RecommendedClientVersion
        $cardDefinitionVersion = Format-OptionalValue $serverStatus.ClientCompatibility.CardDefinitionVersion
        $versionEnforcementEnabled = $serverStatus.ClientCompatibility.VersionEnforcementEnabled -eq $true

        if (-not [string]::IsNullOrWhiteSpace($ExpectedRequiredClientVersion) -and
            $serverStatus.ClientCompatibility.RequiredClientVersion -ne $ExpectedRequiredClientVersion) {
            Add-SmokeFailure $failures "RequiredClientVersion=$($serverStatus.ClientCompatibility.RequiredClientVersion), expected $ExpectedRequiredClientVersion."
        }

        if (-not [string]::IsNullOrWhiteSpace($ExpectedRequiredClientVersion) -and -not $versionEnforcementEnabled) {
            Add-SmokeFailure $failures "Client version enforcement is disabled, but ExpectedRequiredClientVersion was provided."
        }

        Write-Host "[public-smoke] client required=$requiredClientVersion recommended=$recommendedClientVersion cardDb=$cardDefinitionVersion enforcement=$versionEnforcementEnabled"
    }

    if ($null -eq $serverStatus.RateLimits) {
        Add-SmokeFailure $failures "RateLimits block is missing from /server/status. Restart the server with the current build."
    }
    else {
        if ($serverStatus.RateLimits.Enabled -ne $true) {
            Add-SmokeFailure $failures "RateLimits.Enabled is not true. Public PvP requires request limiting."
        }

        Write-Host "[public-smoke] rateLimits enabled=$($serverStatus.RateLimits.Enabled) window=$($serverStatus.RateLimits.WindowSeconds)s auth=$($serverStatus.RateLimits.AuthPermitLimit) account=$($serverStatus.RateLimits.AccountApiPermitLimit) battleConnect=$($serverStatus.RateLimits.BattleConnectPermitLimit) partition=$($serverStatus.RateLimits.PartitionKey)"
    }
}

try {
    Write-Host "[public-smoke] GET /sessions"
    $sessions = Invoke-JsonGet "$BaseUrl/sessions" $TimeoutSeconds
    $sessionJson = $sessions | ConvertTo-Json -Compress -Depth 6
    if ($sessionJson.Length -gt 300) {
        $sessionJson = $sessionJson.Substring(0, 300) + "..."
    }

    Write-Host "[public-smoke] sessions reachable. sample=$sessionJson"
}
catch {
    Add-SmokeWarning $warnings "/sessions request failed: $($_.Exception.Message)"
}

if (-not $SkipLocalLogCheck) {
    Write-Host "[public-smoke] Checking local log paths. Skip with -SkipLocalLogCheck when running from a remote client PC."

    if ([string]::IsNullOrWhiteSpace($ServerLogDirectory) -or -not (Test-Path -LiteralPath $ServerLogDirectory)) {
        Write-Host "[public-smoke] server log directory not found yet: $ServerLogDirectory"
    }
    else {
        $latestOut = Get-LatestProject333LogFile $ServerLogDirectory "server_*.out.log"
        $latestErr = Get-LatestProject333LogFile $ServerLogDirectory "server_*.err.log"
        Write-Host "[public-smoke] latest stdout log=$(if ($null -eq $latestOut) { '-' } else { $latestOut.FullName })"
        Write-Host "[public-smoke] latest stderr log=$(if ($null -eq $latestErr) { '-' } else { $latestErr.FullName })"
    }

    if ([string]::IsNullOrWhiteSpace($AuditLogDirectory) -or -not (Test-Path -LiteralPath $AuditLogDirectory)) {
        Write-Host "[public-smoke] audit log directory not found yet: $AuditLogDirectory"
    }
    else {
        $latestAudit = Get-LatestProject333LogFile $AuditLogDirectory "audit_*.jsonl"
        Write-Host "[public-smoke] latest audit log=$(if ($null -eq $latestAudit) { '-' } else { $latestAudit.FullName })"
    }
}
else {
    Write-Host "[public-smoke] Local log path check skipped."
}

if (-not $SkipWebSocket) {
    try {
        Write-Host "[public-smoke] WebSocket connect /battle"
        Test-Project333WebSocket $BattleWebSocketUrl $TimeoutSeconds
    }
    catch {
        Add-SmokeFailure $failures "WebSocket check failed: $($_.Exception.Message)"
    }
}
else {
    Write-Host "[public-smoke] WebSocket check skipped."
}

Write-Host "[public-smoke] Manual two-client checks still required:"
Write-Host "  1. Two different accounts can log in."
Write-Host "  2. Both accounts have a 33-card draft deck."
Write-Host "  3. PvP matchmaking starts the same battle on both clients."
Write-Host "  4. Unit play, move, spell, attack, and end turn sync on both clients."
Write-Host "  5. Reconnect within 60 seconds restores the same battle."
Write-Host "  6. Reconnect timeout records the correct forfeit result."

if ($failures.Count -gt 0) {
    Write-Host "[public-smoke] Public PvP smoke readiness failed with $($failures.Count) issue(s)."
    foreach ($failure in $failures) {
        Write-Host "  - $failure"
    }

    throw "Project333 public PvP smoke readiness check failed."
}

if ($warnings.Count -gt 0) {
    Write-Host "[public-smoke] Public PvP smoke readiness passed with $($warnings.Count) warning(s)."
}
else {
    Write-Host "[public-smoke] Public PvP smoke readiness passed."
}
