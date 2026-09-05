param(
    [string]$BaseUrl = "http://127.0.0.1:7333",
    [string]$BattleWebSocketUrl = "",
    [string]$ExpectedRequiredClientVersion = "",
    [string]$ExpectedCardDefinitionVersion = "",
    [int]$TimeoutSeconds = 5,
    [switch]$SkipWebSocket
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
    } elseif ($HttpUrl.StartsWith("https://", [System.StringComparison]::OrdinalIgnoreCase)) {
        $webSocketUrl = "wss://" + $HttpUrl.Substring("https://".Length)
    } elseif ($HttpUrl.StartsWith("http://", [System.StringComparison]::OrdinalIgnoreCase)) {
        $webSocketUrl = "ws://" + $HttpUrl.Substring("http://".Length)
    } else {
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

function Test-ExpectedValue {
    param(
        [string]$Name,
        [string]$Actual,
        [string]$Expected
    )

    if ([string]::IsNullOrWhiteSpace($Expected)) {
        return
    }

    if ($Actual -ne $Expected) {
        throw "$Name=$Actual, expected $Expected."
    }
}

$BaseUrl = Normalize-HttpUrl $BaseUrl
$BattleWebSocketUrl = Resolve-BattleWebSocketUrl $BaseUrl $BattleWebSocketUrl

Write-Host "[endpoint] BaseUrl=$BaseUrl"
Write-Host "[endpoint] BattleWebSocketUrl=$BattleWebSocketUrl"

Write-Host "[endpoint] GET /health"
$health = Invoke-JsonGet "$BaseUrl/health" $TimeoutSeconds
Write-Host "[endpoint] health status=$($health.Status) serverTime=$($health.ServerTimeUtc)"
if ($health.Status -ne "Ok") {
    throw "/health returned Status=$($health.Status), expected Ok."
}

Write-Host "[endpoint] GET /server/status"
$serverStatus = Invoke-JsonGet "$BaseUrl/server/status" $TimeoutSeconds
$databaseStatus = if ($null -ne $serverStatus.Database) { $serverStatus.Database.Status } else { "-" }
$cardCount = if ($null -ne $serverStatus.Cards) { $serverStatus.Cards.CardCount } else { "-" }
$deploymentStatus = if ($null -ne $serverStatus.Deployment) { $serverStatus.Deployment.Status } else { "-" }
$pvpReconnectGrace = if ($null -ne $serverStatus.PvP) { $serverStatus.PvP.ReconnectGraceSeconds } else { "-" }
Write-Host "[endpoint] server status=$($serverStatus.Status) db=$databaseStatus cards=$cardCount deployment=$deploymentStatus reconnectGrace=$pvpReconnectGrace"
if ($serverStatus.Status -ne "Ok") {
    throw "/server/status returned Status=$($serverStatus.Status), expected Ok."
}

if ($null -ne $serverStatus.Deployment -and $serverStatus.Deployment.Warnings) {
    foreach ($warning in $serverStatus.Deployment.Warnings) {
        Write-Host "[endpoint] deployment warning: $warning"
    }
}

if ($null -ne $serverStatus.ClientCompatibility) {
    $requiredClientVersion = Format-OptionalValue $serverStatus.ClientCompatibility.RequiredClientVersion
    $recommendedClientVersion = Format-OptionalValue $serverStatus.ClientCompatibility.RecommendedClientVersion
    $cardDefinitionVersion = Format-OptionalValue $serverStatus.ClientCompatibility.CardDefinitionVersion
    $versionEnforcementEnabled = $serverStatus.ClientCompatibility.VersionEnforcementEnabled -eq $true

    Write-Host "[endpoint] client required=$requiredClientVersion recommended=$recommendedClientVersion cardDb=$cardDefinitionVersion enforcement=$versionEnforcementEnabled"

    Test-ExpectedValue "RequiredClientVersion" "$($serverStatus.ClientCompatibility.RequiredClientVersion)" $ExpectedRequiredClientVersion
    Test-ExpectedValue "CardDefinitionVersion" "$($serverStatus.ClientCompatibility.CardDefinitionVersion)" $ExpectedCardDefinitionVersion

    if (-not [string]::IsNullOrWhiteSpace($ExpectedRequiredClientVersion) -and -not $versionEnforcementEnabled) {
        throw "Client version enforcement is disabled, but ExpectedRequiredClientVersion was provided."
    }
}
elseif (-not [string]::IsNullOrWhiteSpace($ExpectedRequiredClientVersion) -or
    -not [string]::IsNullOrWhiteSpace($ExpectedCardDefinitionVersion)) {
    throw "ClientCompatibility block is missing from /server/status."
}

if ($null -eq $serverStatus.RateLimits) {
    throw "RateLimits block is missing from /server/status. Restart with the current server build."
}

Write-Host "[endpoint] rateLimits enabled=$($serverStatus.RateLimits.Enabled) window=$($serverStatus.RateLimits.WindowSeconds)s auth=$($serverStatus.RateLimits.AuthPermitLimit) account=$($serverStatus.RateLimits.AccountApiPermitLimit) battleConnect=$($serverStatus.RateLimits.BattleConnectPermitLimit) partition=$($serverStatus.RateLimits.PartitionKey)"
if ($serverStatus.RateLimits.Enabled -ne $true) {
    throw "RateLimits.Enabled is not true."
}

if ($SkipWebSocket) {
    Write-Host "[endpoint] WebSocket check skipped."
    Write-Host "[endpoint] Server endpoint smoke test passed."
    return
}

Write-Host "[endpoint] WebSocket connect /battle"
$socket = [System.Net.WebSockets.ClientWebSocket]::new()
$cancellation = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))

try {
    $socket.ConnectAsync([System.Uri]$BattleWebSocketUrl, $cancellation.Token).GetAwaiter().GetResult()
    if ($socket.State -ne [System.Net.WebSockets.WebSocketState]::Open) {
        throw "WebSocket did not open. State=$($socket.State)"
    }

    Write-Host "[endpoint] websocket connected."
}
finally {
    if ($socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $closeCancellation = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))
        try {
            $null = $socket.CloseOutputAsync(
                [System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,
                "endpoint smoke test complete",
                $closeCancellation.Token).GetAwaiter().GetResult()
        }
        catch {
            Write-Warning "[endpoint] websocket close warning: $($_.Exception.Message)"
        }
        finally {
            $closeCancellation.Dispose()
        }
    }

    $cancellation.Dispose()
    $socket.Dispose()
}

Write-Host "[endpoint] Server endpoint smoke test passed."
