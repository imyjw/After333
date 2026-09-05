param(
    [string]$BaseUrl = "http://127.0.0.1:7333",
    [Parameter(Mandatory = $true)]
    [string]$PrivateKey,
    [string]$ClientVersion = "0.1.0-dev",
    [string]$AppKey = "",
    [string]$GameId = "",
    [string]$Password = "dev_password_333"
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')

if ([string]::IsNullOrWhiteSpace($PrivateKey)) {
    throw "PrivateKey is required. Use the same value as PROJECT333_LEVELPLAY_PRIVATE_KEY."
}

function Get-Md5Hex {
    param([string]$Value)

    $md5 = [System.Security.Cryptography.MD5]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        $digest = $md5.ComputeHash($bytes)
        return ([System.BitConverter]::ToString($digest)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $md5.Dispose()
    }
}

function Escape-QueryValue {
    param([object]$Value)

    return [System.Uri]::EscapeDataString([string]$Value)
}

if ([string]::IsNullOrWhiteSpace($GameId)) {
    $GameId = "reward.$([guid]::NewGuid().ToString('N').Substring(0, 10))"
}

Write-Host "[rewarded-ad] POST /auth/register gameId=$GameId"
$authBody = @{
    gameId = $GameId
    password = $Password
    displayName = $GameId
    clientVersion = $ClientVersion
} | ConvertTo-Json -Compress

$auth = Invoke-RestMethod `
    -Method Post `
    -Uri "$BaseUrl/auth/register" `
    -ContentType "application/json" `
    -Body $authBody

if ([string]::IsNullOrWhiteSpace($auth.sessionToken)) {
    throw "Game ID registration did not include sessionToken."
}

$headers = @{
    Authorization = "Bearer $($auth.sessionToken)"
}
$ticketsBefore = [int]$auth.wallet.tickets
$providerUserId = [Guid]::NewGuid().ToString("N")

Write-Host "[rewarded-ad] POST /ads/rewarded-ticket/attempt"
$attemptBody = @{
    providerUserId = $providerUserId
} | ConvertTo-Json -Compress

$attempt = Invoke-RestMethod `
    -Method Post `
    -Uri "$BaseUrl/ads/rewarded-ticket/attempt" `
    -Headers $headers `
    -ContentType "application/json" `
    -Body $attemptBody

if (-not $attempt.canShow) {
    throw "Server rejected the rewarded ad attempt. reason=$($attempt.reasonCode) message=$($attempt.message)"
}

if ([string]::IsNullOrWhiteSpace($attempt.attemptId) -or
    [string]::IsNullOrWhiteSpace($attempt.dynamicUserId)) {
    throw "Rewarded ad attempt did not include attemptId and dynamicUserId."
}

$eventId = [Guid]::NewGuid().ToString("N")
$timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString(
    [System.Globalization.CultureInfo]::InvariantCulture)
$rewardCount = [int]$attempt.rewardTicketCount
$signaturePayload = "$timestamp$eventId$providerUserId$rewardCount$PrivateKey"
$signature = Get-Md5Hex $signaturePayload

$query = @(
    "eventId=$(Escape-QueryValue $eventId)",
    "userId=$(Escape-QueryValue $providerUserId)",
    "dynamicUserId=$(Escape-QueryValue $attempt.dynamicUserId)",
    "rewards=$(Escape-QueryValue $rewardCount)",
    "timestamp=$(Escape-QueryValue $timestamp)",
    "signature=$(Escape-QueryValue $signature)",
    "placementName=$(Escape-QueryValue $attempt.placement)"
)

if (-not [string]::IsNullOrWhiteSpace($AppKey)) {
    $query += "appKey=$(Escape-QueryValue $AppKey)"
}

$callbackUrl = "$BaseUrl/ads/levelplay/rewarded-callback?$($query -join '&')"

Write-Host "[rewarded-ad] GET signed LevelPlay callback"
$callback = Invoke-WebRequest -Method Get -Uri $callbackUrl -UseBasicParsing
$expectedAck = "$eventId`:OK"
if ($callback.Content.Trim() -ne $expectedAck) {
    throw "Unexpected callback acknowledgement. expected=$expectedAck actual=$($callback.Content.Trim())"
}

Write-Host "[rewarded-ad] GET attempt result"
$result = Invoke-RestMethod `
    -Method Get `
    -Uri "$BaseUrl/ads/rewarded-ticket/attempt/$($attempt.attemptId)" `
    -Headers $headers

$expectedTickets = $ticketsBefore + $rewardCount
if ($result.status -ne "granted") {
    throw "Rewarded ad attempt was not granted. status=$($result.status) reason=$($result.reasonCode)"
}

if ([int]$result.wallet.tickets -ne $expectedTickets) {
    throw "Ticket count mismatch after reward. expected=$expectedTickets actual=$($result.wallet.tickets)"
}

Write-Host "[rewarded-ad] Replaying the same callback to verify idempotency"
$duplicate = Invoke-WebRequest -Method Get -Uri $callbackUrl -UseBasicParsing
if ($duplicate.Content.Trim() -ne $expectedAck) {
    throw "Duplicate callback did not receive the expected acknowledgement."
}

$afterDuplicate = Invoke-RestMethod `
    -Method Get `
    -Uri "$BaseUrl/ads/rewarded-ticket/attempt/$($attempt.attemptId)" `
    -Headers $headers

if ([int]$afterDuplicate.wallet.tickets -ne $expectedTickets) {
    throw "Duplicate callback granted tickets more than once. expected=$expectedTickets actual=$($afterDuplicate.wallet.tickets)"
}

Write-Host "[rewarded-ad] Passed. tickets=$ticketsBefore -> $expectedTickets, duplicate callback granted 0 extra tickets."
