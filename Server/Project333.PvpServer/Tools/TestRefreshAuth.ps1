param(
    [string]$BaseUrl = "http://127.0.0.1:7333",
    [string]$ClientVersion = "0.1.0-dev",
    [string]$GameId = "",
    [string]$Password = "dev_password_333"
)

$ErrorActionPreference = "Stop"
$base = $BaseUrl.TrimEnd('/')

function Invoke-After333Post {
    param(
        [string]$Path,
        [object]$Body,
        [string]$SessionToken = ""
    )

    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($SessionToken)) {
        $headers.Authorization = "Bearer $SessionToken"
    }

    Invoke-RestMethod `
        -Method Post `
        -Uri "$base$Path" `
        -Headers $headers `
        -ContentType "application/json" `
        -Body ($Body | ConvertTo-Json -Compress)
}

function Assert-Unauthorized {
    param(
        [string]$Path,
        [string]$SessionToken
    )

    try {
        Invoke-WebRequest `
            -Method Get `
            -Uri "$base$Path" `
            -Headers @{ Authorization = "Bearer $SessionToken" } `
            -UseBasicParsing | Out-Null
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 401) {
            return
        }

        throw
    }

    throw "Expected $Path to reject the revoked session token with HTTP 401."
}

if ([string]::IsNullOrWhiteSpace($GameId)) {
    $GameId = "refresh.$([guid]::NewGuid().ToString('N').Substring(0, 10))"
}

Write-Host "[auth-refresh] POST /auth/register gameId=$GameId"
$registered = Invoke-After333Post -Path "/auth/register" -Body @{
    gameId = $GameId
    password = $Password
    displayName = $GameId
    clientVersion = $ClientVersion
}

if ([string]::IsNullOrWhiteSpace($registered.sessionToken) -or
    [string]::IsNullOrWhiteSpace($registered.refreshToken)) {
    throw "Game ID registration did not return both sessionToken and refreshToken."
}

$firstSession = $registered.sessionToken
$firstRefresh = $registered.refreshToken
Write-Host "[auth-refresh] account=$($registered.account.id) session and refresh token issued"

Write-Host "[auth-refresh] POST /auth/refresh"
$refreshed = Invoke-After333Post -Path "/auth/refresh" -Body @{
    refreshToken = $firstRefresh
    clientVersion = $ClientVersion
}

if ([string]::IsNullOrWhiteSpace($refreshed.sessionToken) -or
    [string]::IsNullOrWhiteSpace($refreshed.refreshToken)) {
    throw "Refresh did not return a complete replacement token pair."
}

if ($refreshed.sessionToken -eq $firstSession -or $refreshed.refreshToken -eq $firstRefresh) {
    throw "Refresh returned a token that was not rotated."
}

Assert-Unauthorized -Path "/me" -SessionToken $firstSession
Write-Host "[auth-refresh] old session token revoked"

$me = Invoke-RestMethod `
    -Method Get `
    -Uri "$base/me" `
    -Headers @{ Authorization = "Bearer $($refreshed.sessionToken)" }
Write-Host "[auth-refresh] new session restored account=$($me.account.id)"

Write-Host "[auth-refresh] POST /auth/logout"
$logout = Invoke-After333Post `
    -Path "/auth/logout" `
    -Body @{ refreshToken = $refreshed.refreshToken } `
    -SessionToken $refreshed.sessionToken
if (-not $logout.loggedOut) {
    throw "Logout response did not confirm completion."
}

Assert-Unauthorized -Path "/me" -SessionToken $refreshed.sessionToken
Write-Host "[auth-refresh] logout revoked the replacement session"
Write-Host "[auth-refresh] Automatic login refresh smoke test passed."
