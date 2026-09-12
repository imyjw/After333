param(
    [string]$ProjectPath = "C:\Project_333\Server\Project333.PvpServer\Project333.PvpServer.csproj",
    [string]$ListenUrl = "http://0.0.0.0:7333",
    [string]$PublicServerUrl = "",
    [Parameter(Mandatory = $true)]
    [string]$DatabaseConnection,
    [Parameter(Mandatory = $true)]
    [string]$RequiredClientVersion,
    [string]$RecommendedClientVersion = "",
    [string]$CardDefinitionVersion = "",
    [int]$TicketPurchaseGoldCost = 3,
    [int]$SessionTokenDays = 7,
    [int]$RefreshTokenDays = 30,
    [int]$RateLimitWindowSeconds = 60,
    [int]$AuthRateLimitPerWindow = 12,
    [int]$AccountRateLimitPerWindow = 120,
    [int]$BattleConnectRateLimitPerWindow = 20,
    [string]$GoogleClientIds = $env:PROJECT333_GOOGLE_CLIENT_IDS,
    [string]$GoogleDesktopClientId = $env:PROJECT333_GOOGLE_DESKTOP_CLIENT_ID,
    [string]$GoogleDesktopClientSecret = $env:PROJECT333_GOOGLE_DESKTOP_CLIENT_SECRET,
    [string]$LevelPlayAppKey = $env:PROJECT333_LEVELPLAY_APP_KEY,
    [string]$LevelPlayPrivateKey = $env:PROJECT333_LEVELPLAY_PRIVATE_KEY,
    [string]$LevelPlayRewardedPlacement = "start_ticket_reward",
    [int]$RewardedAdDailyLimit = 10,
    [int]$RewardedAdCooldownSeconds = 30,
    [int]$RewardedAdRewardTickets = 1,
    [switch]$DisableStartupRecovery,
    [switch]$VerboseTransportLogs,
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

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "Project file was not found: $ProjectPath"
}

if ([string]::IsNullOrWhiteSpace($DatabaseConnection)) {
    throw "RunServer_Production.ps1 requires -DatabaseConnection. Production-like PvP must not run without PostgreSQL."
}

if ([string]::IsNullOrWhiteSpace($RequiredClientVersion)) {
    throw "RunServer_Production.ps1 requires -RequiredClientVersion so clients can see the expected build version."
}

if ($SessionTokenDays -lt 1 -or $RefreshTokenDays -lt 1) {
    throw "SessionTokenDays and RefreshTokenDays must both be at least 1."
}

if ($RateLimitWindowSeconds -lt 1 -or
    $AuthRateLimitPerWindow -lt 1 -or
    $AccountRateLimitPerWindow -lt 1 -or
    $BattleConnectRateLimitPerWindow -lt 1) {
    throw "Rate-limit window and permit values must all be at least 1."
}

if (-not [string]::IsNullOrWhiteSpace($LevelPlayPrivateKey)) {
    if ($LevelPlayPrivateKey -match '^<.*>$') {
        throw "LevelPlayPrivateKey still contains a placeholder. Enter the private key from the LevelPlay Set S2S callback page."
    }

    if (-not [string]::IsNullOrWhiteSpace($LevelPlayAppKey) -and
        [string]::Equals($LevelPlayPrivateKey, $LevelPlayAppKey, [StringComparison]::Ordinal)) {
        throw "LevelPlayPrivateKey must be the S2S callback private key, not the Android App Key."
    }
}

Set-Project333Env "ASPNETCORE_ENVIRONMENT" "Production"
Set-Project333Env "PROJECT333_PVP_SERVER_URL" $ListenUrl
Set-Project333Env "PROJECT333_PUBLIC_SERVER_URL" $PublicServerUrl
Set-Project333Env "PROJECT333_DB_CONNECTION" $DatabaseConnection
Set-Project333Env "PROJECT333_REQUIRED_CLIENT_VERSION" $RequiredClientVersion
Set-Project333Env "PROJECT333_RECOMMENDED_CLIENT_VERSION" $RecommendedClientVersion
Set-Project333Env "PROJECT333_CARD_DEFINITION_VERSION" $CardDefinitionVersion
Set-Project333Env "PROJECT333_DEV_MIN_TICKETS" ""
Set-Project333Env "PROJECT333_TICKET_PURCHASE_GOLD_COST" "$TicketPurchaseGoldCost"
Set-Project333Env "PROJECT333_SESSION_TOKEN_DAYS" "$SessionTokenDays"
Set-Project333Env "PROJECT333_REFRESH_TOKEN_DAYS" "$RefreshTokenDays"
Set-Project333Env "PROJECT333_ENABLE_PVP_STARTUP_RECOVERY" $(if ($DisableStartupRecovery) { "0" } else { "1" })
Set-Project333Env "PROJECT333_VERBOSE_TRANSPORT_LOGS" $(if ($VerboseTransportLogs) { "1" } else { "0" })
Set-Project333Env "PROJECT333_RATE_LIMIT_ENABLED" "1"
Set-Project333Env "PROJECT333_RATE_LIMIT_WINDOW_SECONDS" "$RateLimitWindowSeconds"
Set-Project333Env "PROJECT333_AUTH_RATE_LIMIT_PER_WINDOW" "$AuthRateLimitPerWindow"
Set-Project333Env "PROJECT333_ACCOUNT_RATE_LIMIT_PER_WINDOW" "$AccountRateLimitPerWindow"
Set-Project333Env "PROJECT333_BATTLE_CONNECT_RATE_LIMIT_PER_WINDOW" "$BattleConnectRateLimitPerWindow"
Set-Project333Env "PROJECT333_GOOGLE_CLIENT_IDS" $GoogleClientIds
Set-Project333Env "PROJECT333_GOOGLE_DESKTOP_CLIENT_ID" $GoogleDesktopClientId
Set-Project333Env "PROJECT333_GOOGLE_DESKTOP_CLIENT_SECRET" $GoogleDesktopClientSecret
Set-Project333Env "PROJECT333_LEVELPLAY_APP_KEY" $LevelPlayAppKey
Set-Project333Env "PROJECT333_LEVELPLAY_PRIVATE_KEY" $LevelPlayPrivateKey
Set-Project333Env "PROJECT333_LEVELPLAY_REWARDED_PLACEMENT" $LevelPlayRewardedPlacement
Set-Project333Env "PROJECT333_REWARDED_AD_DAILY_LIMIT" "$RewardedAdDailyLimit"
Set-Project333Env "PROJECT333_REWARDED_AD_COOLDOWN_SECONDS" "$RewardedAdCooldownSeconds"
Set-Project333Env "PROJECT333_REWARDED_AD_REWARD_TICKETS" "$RewardedAdRewardTickets"

$googleClientIdValues = @()
if (-not [string]::IsNullOrWhiteSpace($env:PROJECT333_GOOGLE_CLIENT_IDS)) {
    $googleClientIdValues = @(
        $env:PROJECT333_GOOGLE_CLIENT_IDS -split '[,;\r\n]+' |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            ForEach-Object { $_.Trim() }
    )
}
$googleClientIdCount = $googleClientIdValues.Count
$googleDesktopExchangeConfigured =
    -not [string]::IsNullOrWhiteSpace($env:PROJECT333_GOOGLE_DESKTOP_CLIENT_ID) -and
    -not [string]::IsNullOrWhiteSpace($env:PROJECT333_GOOGLE_DESKTOP_CLIENT_SECRET) -and
    $googleClientIdValues -ccontains $env:PROJECT333_GOOGLE_DESKTOP_CLIENT_ID.Trim()

Write-Host "[After333] Production-like server settings"
Write-Host "  ListenUrl:       $env:PROJECT333_PVP_SERVER_URL"
Write-Host "  PublicUrl:       $(if ([string]::IsNullOrWhiteSpace($env:PROJECT333_PUBLIC_SERVER_URL)) { '<same as ListenUrl>' } else { $env:PROJECT333_PUBLIC_SERVER_URL })"
Write-Host "  Database:        $(Format-MaskedConnectionString $env:PROJECT333_DB_CONNECTION)"
Write-Host "  Client:          required=$env:PROJECT333_REQUIRED_CLIENT_VERSION recommended=$env:PROJECT333_RECOMMENDED_CLIENT_VERSION cardDb=$env:PROJECT333_CARD_DEFINITION_VERSION"
Write-Host "  Tickets:         devMinimum=<disabled> purchaseGoldCost=$env:PROJECT333_TICKET_PURCHASE_GOLD_COST"
Write-Host "  Auth:            session=${env:PROJECT333_SESSION_TOKEN_DAYS}d refresh=${env:PROJECT333_REFRESH_TOKEN_DAYS}d"
Write-Host "  Recovery:        startup=$env:PROJECT333_ENABLE_PVP_STARTUP_RECOVERY verboseTransport=$env:PROJECT333_VERBOSE_TRANSPORT_LOGS"
Write-Host "  RateLimits:      enabled=$env:PROJECT333_RATE_LIMIT_ENABLED window=${env:PROJECT333_RATE_LIMIT_WINDOW_SECONDS}s auth=$env:PROJECT333_AUTH_RATE_LIMIT_PER_WINDOW account=$env:PROJECT333_ACCOUNT_RATE_LIMIT_PER_WINDOW battleConnect=$env:PROJECT333_BATTLE_CONNECT_RATE_LIMIT_PER_WINDOW"
Write-Host "  GoogleAuth:      configured=$($googleClientIdCount -gt 0) acceptedClientIds=$googleClientIdCount desktopCodeExchange=$googleDesktopExchangeConfigured"
Write-Host "  RewardedAds:     configured=$(-not [string]::IsNullOrWhiteSpace($env:PROJECT333_LEVELPLAY_PRIVATE_KEY)) placement=$env:PROJECT333_LEVELPLAY_REWARDED_PLACEMENT rewardTickets=$env:PROJECT333_REWARDED_AD_REWARD_TICKETS dailyLimit=$env:PROJECT333_REWARDED_AD_DAILY_LIMIT cooldown=${env:PROJECT333_REWARDED_AD_COOLDOWN_SECONDS}s"
Write-Host "  Environment:     $env:ASPNETCORE_ENVIRONMENT"
Write-Host ""
Write-Host "[After333] Notes"
Write-Host "  - Keep database passwords out of committed files."
Write-Host "  - Set PublicServerUrl to the HTTPS address used by clients when a tunnel or reverse proxy fronts the local listener."
Write-Host "  - After startup, open /server/status and confirm Deployment.Ready is true before public testing."

if ($PrintOnly) {
    Write-Host "[After333] PrintOnly was set. Server was not started."
    return
}

dotnet run --project $ProjectPath
