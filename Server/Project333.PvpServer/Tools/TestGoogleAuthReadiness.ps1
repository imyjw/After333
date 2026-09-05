param(
    [string]$BaseUrl = "http://127.0.0.1:7333",
    [string]$IdToken = "",
    [string]$SessionToken = "",
    [string]$ClientVersion = "0.1.0-dev",
    [switch]$LinkCurrentAccount,
    [switch]$RequireDesktopCodeExchange
)

$ErrorActionPreference = "Stop"
$baseUrlValue = $BaseUrl.TrimEnd('/')

Write-Host "[google-auth] GET $baseUrlValue/server/status"
$status = Invoke-RestMethod -Uri "$baseUrlValue/server/status" -Method Get
$google = $status.Authentication.Google
if ($null -eq $google) {
    throw "The server status response does not contain Authentication.Google. Restart the updated server."
}

Write-Host "[google-auth] configured=$($google.Configured) acceptedClientIds=$($google.AcceptedClientIdCount) desktopCodeExchange=$($google.DesktopCodeExchangeConfigured)"
if (-not $google.Configured) {
    throw "Google login is not configured. Set PROJECT333_GOOGLE_CLIENT_IDS and restart the server."
}

if ($RequireDesktopCodeExchange -and -not $google.DesktopCodeExchangeConfigured) {
    throw "Windows Google login is not configured. Set PROJECT333_GOOGLE_DESKTOP_CLIENT_ID and PROJECT333_GOOGLE_DESKTOP_CLIENT_SECRET, include the desktop ID in PROJECT333_GOOGLE_CLIENT_IDS, and restart the server."
}

if ([string]::IsNullOrWhiteSpace($IdToken)) {
    Write-Host "[google-auth] Readiness check passed. Supply -IdToken only after the Unity Google SDK returns a real ID token."
    return
}

$headers = @{}
$path = "/auth/google"
$requestBody = @{
    idToken = $IdToken
    clientVersion = $ClientVersion
}

if ($LinkCurrentAccount) {
    if ([string]::IsNullOrWhiteSpace($SessionToken)) {
        throw "-SessionToken is required with -LinkCurrentAccount."
    }

    $path = "/auth/link-google"
    $headers.Authorization = "Bearer $SessionToken"
    $requestBody = @{ idToken = $IdToken }
}

Write-Host "[google-auth] POST $path (token hidden)"
$response = Invoke-RestMethod `
    -Uri "$baseUrlValue$path" `
    -Method Post `
    -Headers $headers `
    -ContentType "application/json" `
    -Body ($requestBody | ConvertTo-Json -Compress)

Write-Host "[google-auth] success account=$($response.Account.Id) displayName=$($response.Account.DisplayName) kind=$($response.Account.AccountKind)"
