param(
    [string]$BaseUrl = "http://127.0.0.1:7333",
    [string]$BattleWebSocketUrl = "",
    [string]$ExpectedPublicUrl = "",
    [string]$ExpectedRequiredClientVersion = "",
    [string]$ExpectedCardDefinitionVersion = "",
    [int]$TimeoutSeconds = 8,
    [switch]$AllowDeploymentWarnings,
    [switch]$RequireStartupRecovery,
    [switch]$SkipWebSocket,
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

function Add-ReadinessFailure {
    param(
        [System.Collections.Generic.List[string]]$Failures,
        [string]$Message
    )

    $Failures.Add($Message) | Out-Null
    Write-Host "[readiness] FAIL: $Message"
}

function Test-Project333WebSocket {
    param(
        [string]$Url,
        [int]$TimeoutSeconds
    )

    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $cancellation = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))

    try {
        $socket.ConnectAsync([System.Uri]$Url, $cancellation.Token).GetAwaiter().GetResult()
        if ($socket.State -ne [System.Net.WebSockets.WebSocketState]::Open) {
            throw "WebSocket did not open. State=$($socket.State)"
        }

        Write-Host "[readiness] websocket connected."
    }
    finally {
        if ($socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
            $closeCancellation = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))
            try {
                $null = $socket.CloseOutputAsync(
                    [System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,
                    "deployment readiness test complete",
                    $closeCancellation.Token).GetAwaiter().GetResult()
            }
            catch {
                Write-Warning "[readiness] websocket close warning: $($_.Exception.Message)"
            }
            finally {
                $closeCancellation.Dispose()
            }
        }

        $cancellation.Dispose()
        $socket.Dispose()
    }
}

$BaseUrl = Normalize-HttpUrl $BaseUrl
$BattleWebSocketUrl = Resolve-BattleWebSocketUrl $BaseUrl $BattleWebSocketUrl

Write-Host "[readiness] BaseUrl=$BaseUrl"
Write-Host "[readiness] BattleWebSocketUrl=$BattleWebSocketUrl"
Write-Host "[readiness] ExpectedPublicUrl=$(if ([string]::IsNullOrWhiteSpace($ExpectedPublicUrl)) { '-' } else { $ExpectedPublicUrl.Trim().TrimEnd('/') })"
Write-Host "[readiness] AllowDeploymentWarnings=$AllowDeploymentWarnings RequireStartupRecovery=$RequireStartupRecovery"

if ($PrintOnly) {
    Write-Host "[readiness] PrintOnly was set. No endpoint checks were started."
    return
}

$failures = [System.Collections.Generic.List[string]]::new()

try {
    Write-Host "[readiness] GET /health"
    $health = Invoke-JsonGet "$BaseUrl/health" $TimeoutSeconds
    if ($health.Status -ne "Ok") {
        Add-ReadinessFailure $failures "/health returned Status=$($health.Status), expected Ok."
    }
    else {
        Write-Host "[readiness] health ok. serverTime=$($health.ServerTimeUtc)"
    }
}
catch {
    Add-ReadinessFailure $failures "/health request failed: $($_.Exception.Message)"
}

$serverStatus = $null
try {
    Write-Host "[readiness] GET /server/status"
    $serverStatus = Invoke-JsonGet "$BaseUrl/server/status" $TimeoutSeconds
    if ($serverStatus.Status -ne "Ok") {
        Add-ReadinessFailure $failures "/server/status returned Status=$($serverStatus.Status), expected Ok."
    }
    else {
        Write-Host "[readiness] server status ok."
    }
}
catch {
    Add-ReadinessFailure $failures "/server/status request failed: $($_.Exception.Message)"
}

if ($null -ne $serverStatus) {
    $actualPublicUrl = "$($serverStatus.PublicUrl)".Trim().TrimEnd('/')
    Write-Host "[readiness] server listen=$($serverStatus.ListenUrl) public=$actualPublicUrl"
    if (-not [string]::IsNullOrWhiteSpace($ExpectedPublicUrl) -and
        $actualPublicUrl -ne $ExpectedPublicUrl.Trim().TrimEnd('/')) {
        Add-ReadinessFailure $failures "PublicUrl=$actualPublicUrl, expected $($ExpectedPublicUrl.Trim().TrimEnd('/'))."
    }

    if ($null -eq $serverStatus.Database) {
        Add-ReadinessFailure $failures "Database block is missing from /server/status."
    }
    else {
        if ($serverStatus.Database.Status -ne "Ok") {
            Add-ReadinessFailure $failures "Database.Status=$($serverStatus.Database.Status), expected Ok."
        }

        if ($serverStatus.Database.Configured -ne $true) {
            Add-ReadinessFailure $failures "Database.Configured is not true."
        }

        if ($serverStatus.Database.Reachable -ne $true) {
            Add-ReadinessFailure $failures "Database.Reachable is not true."
        }

        Write-Host "[readiness] database status=$($serverStatus.Database.Status) migrations=$($serverStatus.Database.AppliedMigrationCount) latest=$($serverStatus.Database.LatestMigration)"
    }

    if ($null -eq $serverStatus.Cards) {
        Add-ReadinessFailure $failures "Cards block is missing from /server/status."
    }
    else {
        if ($serverStatus.Cards.Status -ne "Ok") {
            Add-ReadinessFailure $failures "Cards.Status=$($serverStatus.Cards.Status), expected Ok."
        }

        if ($serverStatus.Cards.CardCount -le 0) {
            Add-ReadinessFailure $failures "Cards.CardCount=$($serverStatus.Cards.CardCount), expected more than 0."
        }

        Write-Host "[readiness] cards status=$($serverStatus.Cards.Status) count=$($serverStatus.Cards.CardCount) schema=$($serverStatus.Cards.SchemaVersion)"
    }

    if ($null -eq $serverStatus.Deployment) {
        Add-ReadinessFailure $failures "Deployment block is missing from /server/status."
    }
    else {
        $warnings = @($serverStatus.Deployment.Warnings)
        if ($warnings.Count -gt 0) {
            foreach ($warning in $warnings) {
                Write-Host "[readiness] deployment warning: $warning"
            }

            if (-not $AllowDeploymentWarnings) {
                Add-ReadinessFailure $failures "Deployment has $($warnings.Count) warning(s). Rerun with -AllowDeploymentWarnings only for local/LAN tests."
            }
        }

        if ($serverStatus.Deployment.Ready -ne $true -and -not $AllowDeploymentWarnings) {
            Add-ReadinessFailure $failures "Deployment.Ready is not true."
        }

        Write-Host "[readiness] deployment status=$($serverStatus.Deployment.Status) mode=$($serverStatus.Deployment.Mode) ready=$($serverStatus.Deployment.Ready)"
    }

    if ($null -eq $serverStatus.PvP) {
        Add-ReadinessFailure $failures "PvP block is missing from /server/status."
    }
    else {
        if ($serverStatus.PvP.ReconnectGraceSeconds -ne 60) {
            Add-ReadinessFailure $failures "PvP.ReconnectGraceSeconds=$($serverStatus.PvP.ReconnectGraceSeconds), expected 60."
        }

        if ($RequireStartupRecovery -and $serverStatus.PvP.StartupRecoveryEnabled -ne $true) {
            Add-ReadinessFailure $failures "PvP.StartupRecoveryEnabled is not true."
        }

        Write-Host "[readiness] pvp reconnectGrace=$($serverStatus.PvP.ReconnectGraceSeconds) startupRecovery=$($serverStatus.PvP.StartupRecoveryEnabled) verboseTransport=$($serverStatus.PvP.VerboseTransportLogsEnabled)"
    }

    if ($null -eq $serverStatus.Audit) {
        Add-ReadinessFailure $failures "Audit block is missing from /server/status."
    }
    else {
        if ($serverStatus.Audit.Enabled -ne $true) {
            Write-Host "[readiness] audit warning: audit logging is disabled."
        }

        Write-Host "[readiness] audit enabled=$($serverStatus.Audit.Enabled) retentionDays=$($serverStatus.Audit.RetentionDays) dir=$($serverStatus.Audit.LogDirectory)"
    }

    if ($null -eq $serverStatus.ClientCompatibility) {
        Add-ReadinessFailure $failures "ClientCompatibility block is missing from /server/status."
    }
    else {
        $versionEnforcementEnabled = $serverStatus.ClientCompatibility.VersionEnforcementEnabled -eq $true

        if (-not [string]::IsNullOrWhiteSpace($ExpectedRequiredClientVersion) -and
            $serverStatus.ClientCompatibility.RequiredClientVersion -ne $ExpectedRequiredClientVersion) {
            Add-ReadinessFailure $failures "RequiredClientVersion=$($serverStatus.ClientCompatibility.RequiredClientVersion), expected $ExpectedRequiredClientVersion."
        }

        if (-not [string]::IsNullOrWhiteSpace($ExpectedRequiredClientVersion) -and -not $versionEnforcementEnabled) {
            Add-ReadinessFailure $failures "Client version enforcement is disabled, but ExpectedRequiredClientVersion was provided."
        }

        if (-not [string]::IsNullOrWhiteSpace($ExpectedCardDefinitionVersion) -and
            $serverStatus.ClientCompatibility.CardDefinitionVersion -ne $ExpectedCardDefinitionVersion) {
            Add-ReadinessFailure $failures "CardDefinitionVersion=$($serverStatus.ClientCompatibility.CardDefinitionVersion), expected $ExpectedCardDefinitionVersion."
        }

        Write-Host "[readiness] client required=$($serverStatus.ClientCompatibility.RequiredClientVersion) recommended=$($serverStatus.ClientCompatibility.RecommendedClientVersion) cardDb=$($serverStatus.ClientCompatibility.CardDefinitionVersion) enforcement=$versionEnforcementEnabled"
    }

    if ($null -eq $serverStatus.RateLimits) {
        Add-ReadinessFailure $failures "RateLimits block is missing from /server/status. Restart with the current server build."
    }
    else {
        if ($serverStatus.RateLimits.Enabled -ne $true) {
            Add-ReadinessFailure $failures "RateLimits.Enabled is not true."
        }

        if ($serverStatus.RateLimits.WindowSeconds -lt 1 -or
            $serverStatus.RateLimits.AuthPermitLimit -lt 1 -or
            $serverStatus.RateLimits.AccountApiPermitLimit -lt 1 -or
            $serverStatus.RateLimits.BattleConnectPermitLimit -lt 1) {
            Add-ReadinessFailure $failures "One or more rate-limit values are invalid."
        }

        Write-Host "[readiness] rateLimits enabled=$($serverStatus.RateLimits.Enabled) window=$($serverStatus.RateLimits.WindowSeconds)s auth=$($serverStatus.RateLimits.AuthPermitLimit) account=$($serverStatus.RateLimits.AccountApiPermitLimit) battleConnect=$($serverStatus.RateLimits.BattleConnectPermitLimit) partition=$($serverStatus.RateLimits.PartitionKey)"
    }
}

if (-not $SkipWebSocket) {
    try {
        Write-Host "[readiness] WebSocket connect /battle"
        Test-Project333WebSocket $BattleWebSocketUrl $TimeoutSeconds
    }
    catch {
        Add-ReadinessFailure $failures "WebSocket check failed: $($_.Exception.Message)"
    }
}
else {
    Write-Host "[readiness] WebSocket check skipped."
}

if ($failures.Count -gt 0) {
    Write-Host "[readiness] Deployment readiness check failed with $($failures.Count) issue(s)."
    foreach ($failure in $failures) {
        Write-Host "  - $failure"
    }

    throw "After333 deployment readiness check failed."
}

Write-Host "[readiness] After333 deployment readiness check passed."
