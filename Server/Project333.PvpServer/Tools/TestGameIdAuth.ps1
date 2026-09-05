param(
    [string]$BaseUrl = "http://127.0.0.1:7333",
    [string]$GameId = "",
    [string]$Password = "dev_password_333"
)

$ErrorActionPreference = "Stop"

function Format-MaskedToken {
    param([string]$Token)

    if ([string]::IsNullOrWhiteSpace($Token)) {
        return "<empty>"
    }

    if ($Token.Length -le 10) {
        return "****"
    }

    return "$($Token.Substring(0, 6))...$($Token.Substring($Token.Length - 4))"
}

if ([string]::IsNullOrWhiteSpace($GameId)) {
    $GameId = "dev.$([guid]::NewGuid().ToString('N').Substring(0, 10))"
}

$registerBody = @{
    gameId = $GameId
    password = $Password
    displayName = $GameId
    clientVersion = "dev-smoke-test"
} | ConvertTo-Json -Compress

Write-Host "[auth] POST /auth/register gameId=$GameId"
$registerResponse = Invoke-RestMethod `
    -Method Post `
    -Uri "$BaseUrl/auth/register" `
    -ContentType "application/json" `
    -Body $registerBody

if ([string]::IsNullOrWhiteSpace($registerResponse.sessionToken)) {
    throw "Register response did not include sessionToken."
}

Write-Host "[auth] registered account=$($registerResponse.account.id) displayName=$($registerResponse.account.displayName) sessionToken=$(Format-MaskedToken $registerResponse.sessionToken)"

$loginBody = @{
    gameId = $GameId
    password = $Password
    clientVersion = "dev-smoke-test"
} | ConvertTo-Json -Compress

Write-Host "[auth] POST /auth/login gameId=$GameId"
$loginResponse = Invoke-RestMethod `
    -Method Post `
    -Uri "$BaseUrl/auth/login" `
    -ContentType "application/json" `
    -Body $loginBody

if ($loginResponse.account.id -ne $registerResponse.account.id) {
    throw "Login returned a different account id. register=$($registerResponse.account.id), login=$($loginResponse.account.id)"
}

Write-Host "[auth] login account=$($loginResponse.account.id) sessionToken=$(Format-MaskedToken $loginResponse.sessionToken)"
Write-Host "[auth] Game ID auth smoke test passed."
