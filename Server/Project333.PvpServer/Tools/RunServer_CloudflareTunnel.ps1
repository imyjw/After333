param(
    [string]$ProjectPath = "C:\Project_333\Server\Project333.PvpServer\Project333.PvpServer.csproj",
    [string]$ListenUrl = "http://127.0.0.1:7333",
    [string]$PublicServerUrl = "https://api.after333.com",
    [Parameter(Mandatory = $true)]
    [string]$DatabaseConnection,
    [string]$RequiredClientVersion = "0.1.0-dev",
    [string]$RecommendedClientVersion = "",
    [string]$CardDefinitionVersion = "",
    [int]$TicketPurchaseGoldCost = 3,
    [int]$SessionTokenDays = 7,
    [int]$RefreshTokenDays = 30,
    [string]$GoogleClientIds = $env:PROJECT333_GOOGLE_CLIENT_IDS,
    [string]$GoogleDesktopClientId = $env:PROJECT333_GOOGLE_DESKTOP_CLIENT_ID,
    [string]$GoogleDesktopClientSecret = $env:PROJECT333_GOOGLE_DESKTOP_CLIENT_SECRET,
    [string]$LevelPlayAppKey = $env:PROJECT333_LEVELPLAY_APP_KEY,
    [string]$LevelPlayPrivateKey = $env:PROJECT333_LEVELPLAY_PRIVATE_KEY,
    [string]$LevelPlayRewardedPlacement = "start_ticket_reward",
    [int]$RewardedAdDailyLimit = 3,
    [int]$RewardedAdCooldownSeconds = 60,
    [int]$RewardedAdRewardTickets = 1,
    [switch]$DisableStartupRecovery,
    [switch]$VerboseTransportLogs,
    [switch]$PrintOnly
)

$ErrorActionPreference = "Stop"

$productionScript = Join-Path $PSScriptRoot "RunServer_Production.ps1"
if (-not (Test-Path -LiteralPath $productionScript)) {
    throw "Production server launcher was not found: $productionScript"
}

try {
    $listenUri = [System.Uri]$ListenUrl
}
catch {
    throw "ListenUrl must be a valid absolute URL: $ListenUrl"
}

if ($listenUri.Host -notin @("127.0.0.1", "localhost", "::1")) {
    throw "Cloudflare Tunnel mode must listen only on loopback. Use http://127.0.0.1:7333 unless the tunnel origin is configured differently."
}

try {
    $publicUri = [System.Uri]$PublicServerUrl
}
catch {
    throw "PublicServerUrl must be a valid absolute URL: $PublicServerUrl"
}

if ($publicUri.Scheme -ne "https") {
    throw "Cloudflare Tunnel public URL must use https:// so clients derive a secure wss:// battle endpoint."
}

$arguments = @{
    ProjectPath = $ProjectPath
    ListenUrl = $ListenUrl
    PublicServerUrl = $PublicServerUrl
    DatabaseConnection = $DatabaseConnection
    RequiredClientVersion = $RequiredClientVersion
    RecommendedClientVersion = $RecommendedClientVersion
    CardDefinitionVersion = $CardDefinitionVersion
    TicketPurchaseGoldCost = $TicketPurchaseGoldCost
    SessionTokenDays = $SessionTokenDays
    RefreshTokenDays = $RefreshTokenDays
    GoogleClientIds = $GoogleClientIds
    GoogleDesktopClientId = $GoogleDesktopClientId
    GoogleDesktopClientSecret = $GoogleDesktopClientSecret
    LevelPlayAppKey = $LevelPlayAppKey
    LevelPlayPrivateKey = $LevelPlayPrivateKey
    LevelPlayRewardedPlacement = $LevelPlayRewardedPlacement
    RewardedAdDailyLimit = $RewardedAdDailyLimit
    RewardedAdCooldownSeconds = $RewardedAdCooldownSeconds
    RewardedAdRewardTickets = $RewardedAdRewardTickets
}

if ($DisableStartupRecovery) {
    $arguments.DisableStartupRecovery = $true
}

if ($VerboseTransportLogs) {
    $arguments.VerboseTransportLogs = $true
}

if ($PrintOnly) {
    $arguments.PrintOnly = $true
}

Write-Host "[After333] Cloudflare Tunnel mode"
Write-Host "  Origin: $ListenUrl"
Write-Host "  Public: $PublicServerUrl"
Write-Host "  Battle: $($PublicServerUrl.TrimEnd('/') -replace '^https://', 'wss://')/battle"
Write-Host ""

& $productionScript @arguments
